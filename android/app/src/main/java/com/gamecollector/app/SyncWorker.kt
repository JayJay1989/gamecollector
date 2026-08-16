package com.gamecollector.app

import android.content.Context
import android.net.Uri
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.gamecollector.core.auth.OidcSessionManager
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.network.GameCollectorApi
import com.gamecollector.core.sync.SyncEngine
import com.gamecollector.core.sync.GameCollectorSyncRemote
import com.gamecollector.core.sync.SyncRunResult
import java.time.Duration
import java.util.concurrent.TimeUnit

class SyncWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        val session = OidcSessionManager(
            applicationContext,
            Uri.parse(BuildConfig.OIDC_ISSUER),
            BuildConfig.OIDC_CLIENT_ID,
            Uri.parse(BuildConfig.OIDC_REDIRECT_URI),
        )
        return try {
            if (!session.isAuthorized) return Result.success()
            val engine = SyncEngine(
                GameCollectorDatabase.get(applicationContext),
                GameCollectorSyncRemote(GameCollectorApi(BuildConfig.API_BASE_URL, session)),
                InstallationIdStore(applicationContext).id,
            )
            when (engine.run()) {
                SyncRunResult.Success, SyncRunResult.SignedOut -> Result.success()
                SyncRunResult.Retry -> { AppDiagnostics(applicationContext).record("sync", "retry"); Result.retry() }
                SyncRunResult.Failure -> { AppDiagnostics(applicationContext).record("sync", "failed"); Result.failure() }
            }
        } finally {
            session.close()
        }
    }
}

object SyncScheduler {
    private val connected = Constraints.Builder()
        .setRequiredNetworkType(NetworkType.CONNECTED)
        .setRequiresBatteryNotLow(true)
        .build()

    fun enqueue(context: Context) {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(connected)
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, Duration.ofSeconds(10))
            .addTag(TAG)
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork(IMMEDIATE, ExistingWorkPolicy.APPEND_OR_REPLACE, request)
    }

    fun ensurePeriodic(context: Context) {
        val request = PeriodicWorkRequestBuilder<SyncWorker>(6, TimeUnit.HOURS)
            .setConstraints(connected)
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, Duration.ofSeconds(10))
            .addTag(TAG)
            .build()
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(PERIODIC, ExistingPeriodicWorkPolicy.KEEP, request)
    }

    private const val TAG = "game-collector-sync"
    private const val IMMEDIATE = "game-collector-sync-now"
    private const val PERIODIC = "game-collector-sync-periodic"
}
