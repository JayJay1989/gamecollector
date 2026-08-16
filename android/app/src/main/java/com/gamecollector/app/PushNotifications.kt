package com.gamecollector.app

import android.content.Context
import android.app.Application
import android.net.Uri
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import androidx.work.workDataOf
import com.gamecollector.core.auth.OidcSessionManager
import com.gamecollector.core.network.ApiResult
import com.gamecollector.core.network.GameCollectorApi
import com.google.firebase.FirebaseApp
import com.google.firebase.messaging.FirebaseMessaging
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import java.time.Duration
import kotlin.coroutines.resume
import kotlinx.coroutines.suspendCancellableCoroutine

class GameCollectorApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        FirebaseBootstrap.initialize(this)
        val previous = Thread.getDefaultUncaughtExceptionHandler()
        Thread.setDefaultUncaughtExceptionHandler { thread, throwable ->
            AppDiagnostics(this).record("crash", throwable.javaClass.simpleName)
            previous?.uncaughtException(thread, throwable)
        }
    }
}

class PushTokenStore(context: Context) {
    private val preferences = context.getSharedPreferences("push", Context.MODE_PRIVATE)
    var token: String?
        get() = preferences.getString("fcm_token", null)
        set(value) { preferences.edit().putString("fcm_token", value).apply() }
}

object FirebaseBootstrap {
    fun initialize(context: Context): Boolean {
        if (FirebaseApp.getApps(context).isNotEmpty()) return true
        return FirebaseApp.initializeApp(context) != null
    }

    @Suppress("DEPRECATION")
    suspend fun token(context: Context): String? {
        PushTokenStore(context).token?.let { return it }
        if (!initialize(context)) return null
        return suspendCancellableCoroutine { continuation ->
            FirebaseMessaging.getInstance().token.addOnCompleteListener { task ->
                val value = task.result?.takeIf(String::isNotBlank)
                if (value != null) PushTokenStore(context).token = value
                if (continuation.isActive) continuation.resume(value)
            }
        }
    }
}

@Suppress("DEPRECATION", "OVERRIDE_DEPRECATION")
class GameCollectorMessagingService : FirebaseMessagingService() {
    override fun onNewToken(token: String) {
        PushTokenStore(this).token = token
        PushRegistrationScheduler.enqueue(this)
    }

    override fun onMessageReceived(message: RemoteMessage) {
        SyncScheduler.enqueue(this)
    }

    override fun onDeletedMessages() {
        SyncScheduler.enqueue(this)
    }
}

class PushRegistrationWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        val token = PushTokenStore(applicationContext).token ?: return Result.success()
        val session = OidcSessionManager(applicationContext, Uri.parse(BuildConfig.OIDC_ISSUER),
            BuildConfig.OIDC_CLIENT_ID, Uri.parse(BuildConfig.OIDC_REDIRECT_URI))
        return try {
            if (!session.isAuthorized) return Result.success()
            when (GameCollectorApi(BuildConfig.API_BASE_URL, session).activateDevice(InstallationIdStore(applicationContext).id, token)) {
                is ApiResult.Success, ApiResult.SignedOut -> Result.success()
                is ApiResult.NetworkError -> Result.retry()
                is ApiResult.Error -> Result.failure()
            }
        } finally { session.close() }
    }
}

object PushRegistrationScheduler {
    fun enqueue(context: Context) {
        val constraints = Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()
        val work = OneTimeWorkRequestBuilder<PushRegistrationWorker>()
            .setConstraints(constraints)
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, Duration.ofSeconds(10))
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork("push-token-registration", ExistingWorkPolicy.REPLACE, work)
    }
}

class NotificationReadWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        val notificationId = inputData.getString("notification_id") ?: return Result.failure()
        val session = OidcSessionManager(applicationContext, Uri.parse(BuildConfig.OIDC_ISSUER),
            BuildConfig.OIDC_CLIENT_ID, Uri.parse(BuildConfig.OIDC_REDIRECT_URI))
        return try {
            if (!session.isAuthorized) return Result.success()
            val api = GameCollectorApi(BuildConfig.API_BASE_URL, session)
            when (val result = if (notificationId == ALL) api.markAllNotificationsRead(InstallationIdStore(applicationContext).id)
                else api.markNotificationRead(InstallationIdStore(applicationContext).id, notificationId)) {
                is ApiResult.Success, ApiResult.SignedOut -> Result.success()
                is ApiResult.NetworkError -> Result.retry()
                is ApiResult.Error -> if (result.statusCode >= 500 || result.statusCode == 429) Result.retry() else Result.failure()
            }
        } finally { session.close() }
    }

    companion object { const val ALL = "*" }
}

object NotificationReadScheduler {
    fun enqueue(context: Context, notificationId: String) {
        val constraints = Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()
        val work = OneTimeWorkRequestBuilder<NotificationReadWorker>()
            .setInputData(workDataOf("notification_id" to notificationId))
            .setConstraints(constraints)
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, Duration.ofSeconds(10))
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork("notification-read-$notificationId", ExistingWorkPolicy.KEEP, work)
    }
}
