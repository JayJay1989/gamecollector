package com.gamecollector.core.auth

fun interface AccessTokenProvider {
    suspend fun freshAccessToken(): String?
}
