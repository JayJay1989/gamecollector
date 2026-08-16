# AppAuth reads these model fields through Gson-compatible reflection.
-keep class net.openid.appauth.** { *; }
-dontwarn org.conscrypt.**

# Keep Firebase messaging entry points named in the manifest.
-keep class com.gamecollector.app.GameCollectorMessagingService { *; }
-keep class com.gamecollector.app.GameCollectorApplication { *; }
