package com.turnaexpress.gazete

import android.content.ActivityNotFoundException
import android.content.Intent
import android.content.pm.PackageInfo
import android.content.pm.PackageManager
import android.content.pm.Signature
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodCall
import io.flutter.plugin.common.MethodChannel
import java.io.File
import java.io.FileOutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.security.MessageDigest
import javax.net.ssl.HttpsURLConnection

class MainActivity : FlutterActivity() {
    companion object {
        private const val UPDATE_CHANNEL = "com.turnaexpress.gazete/update"
        private const val UPDATE_HOST = "gazete.turnaexpress.com.tr"
        private const val APK_MIME_TYPE = "application/vnd.android.package-archive"
        private const val MAX_APK_BYTES = 200L * 1024L * 1024L
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(
            flutterEngine.dartExecutor.binaryMessenger,
            UPDATE_CHANNEL,
        ).setMethodCallHandler(::handleUpdateMethod)
    }

    private fun handleUpdateMethod(call: MethodCall, result: MethodChannel.Result) {
        when (call.method) {
            "getVersionCode" -> result.success(currentVersionCode())
            "canInstallPackages" -> result.success(canInstallPackages())
            "downloadUpdate" -> downloadUpdate(call, result)
            "installDownloaded" -> {
                val path = call.argument<String>("path")
                val versionCode = call.argument<Number>("versionCode")?.toLong()
                if (path.isNullOrBlank() || versionCode == null || versionCode < 1) {
                    result.error("invalid_apk_path", "APK dosya bilgisi eksik.", null)
                    return
                }
                installApk(File(path), versionCode, result)
            }
            else -> result.notImplemented()
        }
    }

    private fun currentVersionCode(): Long = packageVersionCode(
        packageManager.getPackageInfo(packageName, 0),
    )

    private fun canInstallPackages(): Boolean =
        Build.VERSION.SDK_INT < Build.VERSION_CODES.O ||
            packageManager.canRequestPackageInstalls()

    private fun packageVersionCode(packageInfo: PackageInfo): Long =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            packageInfo.longVersionCode
        } else {
            @Suppress("DEPRECATION")
            packageInfo.versionCode.toLong()
        }

    private fun downloadUpdate(call: MethodCall, result: MethodChannel.Result) {
        val rawUrl = call.argument<String>("url")
        val expectedSha256 = call.argument<String>("sha256")?.lowercase()
        val versionCode = call.argument<Number>("versionCode")?.toLong()
        val expectedSize = call.argument<Number>("size")?.toLong()

        if (rawUrl.isNullOrBlank() ||
            expectedSha256 == null ||
            !expectedSha256.matches(Regex("^[a-f0-9]{64}$")) ||
            versionCode == null ||
            versionCode < 1 ||
            expectedSize == null ||
            expectedSize < 1 ||
            expectedSize > MAX_APK_BYTES
        ) {
            result.error("invalid_update", "Güncelleme bilgileri geçersiz.", null)
            return
        }

        val updateUrl = runCatching { URL(rawUrl) }.getOrNull()
        if (updateUrl == null ||
            updateUrl.protocol.lowercase() != "https" ||
            updateUrl.host.lowercase() != UPDATE_HOST ||
            (updateUrl.port != -1 && updateUrl.port != 443)
        ) {
            result.error("untrusted_update_url", "APK adresi güvenilir değil.", null)
            return
        }

        Thread {
            var connection: HttpsURLConnection? = null
            val updateDirectory = File(cacheDir, "updates")
            val partialFile = File(updateDirectory, "gazete-update-$versionCode.apk.part")
            val apkFile = File(updateDirectory, "gazete-update-$versionCode.apk")
            try {
                if (!updateDirectory.isDirectory && !updateDirectory.mkdirs()) {
                    throw IllegalStateException("Güncelleme klasörü hazırlanamadı.")
                }

                connection = updateUrl.openConnection() as HttpsURLConnection
                connection.instanceFollowRedirects = false
                connection.connectTimeout = 15_000
                connection.readTimeout = 30_000
                connection.requestMethod = "GET"
                connection.setRequestProperty("Cache-Control", "no-cache")
                connection.connect()

                if (connection.responseCode != HttpURLConnection.HTTP_OK) {
                    throw IllegalStateException(
                        "APK indirilemedi (${connection.responseCode}).",
                    )
                }
                val contentLength = connection.contentLengthLong
                if (contentLength > MAX_APK_BYTES ||
                    (contentLength >= 0 && contentLength != expectedSize)
                ) {
                    throw SecurityException("APK dosya boyutu uyuşmuyor.")
                }

                partialFile.delete()
                apkFile.delete()
                val digest = MessageDigest.getInstance("SHA-256")
                var downloadedBytes = 0L
                connection.inputStream.use { input ->
                    FileOutputStream(partialFile).use { output ->
                        val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                        while (true) {
                            val count = input.read(buffer)
                            if (count < 0) break
                            downloadedBytes += count
                            if (downloadedBytes > expectedSize ||
                                downloadedBytes > MAX_APK_BYTES
                            ) {
                                throw SecurityException("APK dosyası beklenenden büyük.")
                            }
                            digest.update(buffer, 0, count)
                            output.write(buffer, 0, count)
                        }
                        output.fd.sync()
                    }
                }
                if (downloadedBytes != expectedSize) {
                    throw SecurityException("APK dosya boyutu uyuşmuyor.")
                }

                val actualSha256 = digest.digest().joinToString("") { byte ->
                    "%02x".format(byte.toInt() and 0xff)
                }
                if (actualSha256 != expectedSha256) {
                    throw SecurityException("APK doğrulaması başarısız.")
                }
                if (!partialFile.renameTo(apkFile)) {
                    throw IllegalStateException("APK dosyası hazırlanamadı.")
                }

                runOnUiThread { installApk(apkFile, versionCode, result) }
            } catch (error: Exception) {
                partialFile.delete()
                apkFile.delete()
                runOnUiThread {
                    result.error(
                        "update_download_failed",
                        error.message ?: "APK indirilemedi.",
                        null,
                    )
                }
            } finally {
                connection?.disconnect()
            }
        }.start()
    }

    private fun installApk(
        apkFile: File,
        expectedVersionCode: Long,
        result: MethodChannel.Result,
    ) {
        val trustedFile = runCatching {
            val canonicalFile = apkFile.canonicalFile
            val canonicalUpdateDirectory = File(cacheDir, "updates").canonicalFile
            if (canonicalFile.parentFile != canonicalUpdateDirectory ||
                !canonicalFile.isFile ||
                !canonicalFile.name.endsWith(".apk", ignoreCase = true)
            ) {
                null
            } else {
                canonicalFile
            }
        }.getOrNull()

        if (trustedFile == null) {
            result.error("invalid_apk_file", "APK dosyası bulunamadı.", null)
            return
        }

        val validationError = validateApk(trustedFile, expectedVersionCode)
        if (validationError != null) {
            trustedFile.delete()
            result.error("untrusted_apk", validationError, null)
            return
        }

        if (!canInstallPackages()) {
            try {
                startActivity(
                    Intent(
                        Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:$packageName"),
                    ),
                )
                result.success(
                    mapOf(
                        "status" to "permission_required",
                        "path" to trustedFile.absolutePath,
                    ),
                )
            } catch (error: ActivityNotFoundException) {
                result.error(
                    "install_permission_unavailable",
                    "Kurulum izni ekranı açılamadı.",
                    null,
                )
            }
            return
        }

        try {
            val contentUri = FileProvider.getUriForFile(
                this,
                "$packageName.fileprovider",
                trustedFile,
            )
            val installIntent = Intent(Intent.ACTION_VIEW).apply {
                setDataAndType(contentUri, APK_MIME_TYPE)
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }
            startActivity(installIntent)
            result.success(
                mapOf(
                    "status" to "started",
                    "path" to trustedFile.absolutePath,
                ),
            )
        } catch (error: Exception) {
            result.error(
                "installer_unavailable",
                error.message ?: "Android kurulum ekranı açılamadı.",
                null,
            )
        }
    }

    private fun validateApk(apkFile: File, expectedVersionCode: Long): String? {
        val archiveInfo = runCatching {
            getArchivePackageInfo(apkFile)
        }.getOrNull() ?: return "APK paketi okunamadı."
        if (archiveInfo.packageName != packageName) {
            return "APK paket adı bu uygulamayla eşleşmiyor."
        }

        val archiveVersionCode = packageVersionCode(archiveInfo)
        if (archiveVersionCode != expectedVersionCode ||
            archiveVersionCode <= currentVersionCode()
        ) {
            return "APK sürümü güncelleme bilgisiyle eşleşmiyor."
        }

        val installedInfo = runCatching { getInstalledPackageInfoWithSignatures() }
            .getOrNull() ?: return "Kurulu uygulama imzası okunamadı."
        val archiveSigners = signerDigests(archiveInfo)
        val installedSigners = signerDigests(installedInfo)
        if (archiveSigners.isEmpty() || archiveSigners != installedSigners) {
            return "APK imzası bu uygulamayla eşleşmiyor."
        }
        return null
    }

    private fun getArchivePackageInfo(apkFile: File): PackageInfo? {
        val flags = signatureFlags()
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            packageManager.getPackageArchiveInfo(
                apkFile.absolutePath,
                PackageManager.PackageInfoFlags.of(flags.toLong()),
            )
        } else {
            @Suppress("DEPRECATION")
            packageManager.getPackageArchiveInfo(apkFile.absolutePath, flags)
        }
    }

    private fun getInstalledPackageInfoWithSignatures(): PackageInfo {
        val flags = signatureFlags()
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            packageManager.getPackageInfo(
                packageName,
                PackageManager.PackageInfoFlags.of(flags.toLong()),
            )
        } else {
            @Suppress("DEPRECATION")
            packageManager.getPackageInfo(packageName, flags)
        }
    }

    private fun signatureFlags(): Int =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            PackageManager.GET_SIGNING_CERTIFICATES
        } else {
            @Suppress("DEPRECATION")
            PackageManager.GET_SIGNATURES
        }

    private fun signerDigests(packageInfo: PackageInfo): Set<String> {
        val signatures: Array<Signature> =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                packageInfo.signingInfo?.apkContentsSigners ?: emptyArray()
            } else {
                @Suppress("DEPRECATION")
                packageInfo.signatures ?: emptyArray()
            }
        return signatures.map { signature ->
            MessageDigest.getInstance("SHA-256")
                .digest(signature.toByteArray())
                .joinToString("") { byte -> "%02x".format(byte.toInt() and 0xff) }
        }.toSet()
    }
}
