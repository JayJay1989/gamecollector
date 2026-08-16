package com.gamecollector.core.auth

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import net.openid.appauth.AuthState
import java.nio.charset.StandardCharsets
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class EncryptedAuthStateStore(context: Context) {
    private val preferences = context.getSharedPreferences("secure_auth_state", Context.MODE_PRIVATE)

    fun read(): AuthState {
        val ciphertext = preferences.getString(CIPHERTEXT, null) ?: return AuthState()
        val initializationVector = preferences.getString(INITIALIZATION_VECTOR, null) ?: return AuthState()
        return runCatching {
            val cipher = Cipher.getInstance(TRANSFORMATION)
            cipher.init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, Base64.decode(initializationVector, Base64.NO_WRAP)))
            val json = String(cipher.doFinal(Base64.decode(ciphertext, Base64.NO_WRAP)), StandardCharsets.UTF_8)
            AuthState.jsonDeserialize(json)
        }.getOrElse {
            clear()
            AuthState()
        }
    }

    fun write(state: AuthState) {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, key())
        val ciphertext = cipher.doFinal(state.jsonSerializeString().toByteArray(StandardCharsets.UTF_8))
        preferences.edit()
            .putString(CIPHERTEXT, Base64.encodeToString(ciphertext, Base64.NO_WRAP))
            .putString(INITIALIZATION_VECTOR, Base64.encodeToString(cipher.iv, Base64.NO_WRAP))
            .apply()
    }

    fun clear() {
        preferences.edit().clear().apply()
    }

    private fun key(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").run {
            init(
                KeyGenParameterSpec.Builder(KEY_ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .build()
            )
            generateKey()
        }
    }

    private companion object {
        const val KEY_ALIAS = "game_collector_auth_state"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
        const val CIPHERTEXT = "ciphertext"
        const val INITIALIZATION_VECTOR = "initialization_vector"
    }
}
