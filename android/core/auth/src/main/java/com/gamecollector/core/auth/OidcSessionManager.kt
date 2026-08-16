package com.gamecollector.core.auth

import android.content.Context
import android.content.Intent
import android.net.Uri
import kotlinx.coroutines.suspendCancellableCoroutine
import net.openid.appauth.AuthState
import net.openid.appauth.AuthorizationException
import net.openid.appauth.AuthorizationRequest
import net.openid.appauth.AuthorizationResponse
import net.openid.appauth.AuthorizationService
import net.openid.appauth.AuthorizationServiceConfiguration
import net.openid.appauth.CodeVerifierUtil
import net.openid.appauth.ResponseTypeValues
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException

class OidcSessionManager(
    context: Context,
    private val issuer: Uri,
    private val clientId: String,
    private val redirectUri: Uri,
    private val store: EncryptedAuthStateStore = EncryptedAuthStateStore(context),
) : AccessTokenProvider, AutoCloseable {
    private val authorizationService = AuthorizationService(context.applicationContext)
    @Volatile private var state: AuthState = store.read()

    val isAuthorized: Boolean get() = state.isAuthorized

    suspend fun authorizationIntent(): Intent {
        val configuration = discoverConfiguration()
        val request = AuthorizationRequest.Builder(
            configuration,
            clientId,
            ResponseTypeValues.CODE,
            redirectUri,
        )
            .setScope("openid profile offline_access")
            .setCodeVerifier(CodeVerifierUtil.generateRandomCodeVerifier())
            .build()
        return authorizationService.getAuthorizationRequestIntent(request)
    }

    suspend fun completeAuthorization(data: Intent) {
        val response = AuthorizationResponse.fromIntent(data)
        val authorizationError = AuthorizationException.fromIntent(data)
        if (response == null) throw authorizationError ?: IllegalStateException("The authorization response is missing.")
        state.update(response, authorizationError)
        val tokenResponse = suspendCancellableCoroutine { continuation ->
            authorizationService.performTokenRequest(response.createTokenExchangeRequest()) { token, error ->
                if (error != null) continuation.resumeWithException(error)
                else continuation.resume(token ?: kotlin.error("The token response is missing."))
            }
        }
        state.update(tokenResponse, null)
        store.write(state)
    }

    override suspend fun freshAccessToken(): String? = suspendCancellableCoroutine { continuation ->
        if (!state.isAuthorized) {
            continuation.resume(null)
            return@suspendCancellableCoroutine
        }
        state.performActionWithFreshTokens(authorizationService) { accessToken, _, error ->
            if (error != null) continuation.resumeWithException(error)
            else {
                store.write(state)
                continuation.resume(accessToken)
            }
        }
    }

    fun signOut() {
        state = AuthState()
        store.clear()
    }

    override fun close() = authorizationService.dispose()

    private suspend fun discoverConfiguration(): AuthorizationServiceConfiguration =
        suspendCancellableCoroutine { continuation ->
            AuthorizationServiceConfiguration.fetchFromIssuer(issuer) { configuration, error ->
                if (error != null) continuation.resumeWithException(error)
                else continuation.resume(configuration ?: kotlin.error("OIDC discovery returned no configuration."))
            }
        }
}
