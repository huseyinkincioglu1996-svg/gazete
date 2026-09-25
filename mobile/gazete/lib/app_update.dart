import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/services.dart';

import 'navigation_policy.dart';

final Uri apkUpdateManifestUri = Uri(
  scheme: 'https',
  host: gazeteHost,
  path: '/downloads/android/update.json',
);

const String appPackageName = 'com.turnaexpress.gazete';
const int _maximumManifestBytes = 64 * 1024;
const int maximumApkBytes = 200 * 1024 * 1024;

class AppUpdateInfo {
  const AppUpdateInfo({
    required this.versionCode,
    required this.versionName,
    required this.apkUri,
    required this.sha256,
    required this.size,
  });

  final int versionCode;
  final String versionName;
  final Uri apkUri;
  final String sha256;
  final int size;

  bool isNewerThan(int currentVersionCode) => versionCode > currentVersionCode;

  factory AppUpdateInfo.fromJson(Object? value) {
    if (value is! Map<String, dynamic>) {
      throw const FormatException('Güncelleme bilgisi geçersiz.');
    }

    final Object? rawVersionCode = value['versionCode'];
    final int? versionCode = rawVersionCode is int
        ? rawVersionCode
        : int.tryParse(rawVersionCode?.toString() ?? '');
    final String versionName = value['versionName']?.toString().trim() ?? '';
    final Uri? apkUri = Uri.tryParse(value['apkUrl']?.toString().trim() ?? '');
    final String sha256 =
        value['sha256']?.toString().trim().toLowerCase() ?? '';
    final Object? rawSize = value['size'];
    final int? size = rawSize is int
        ? rawSize
        : int.tryParse(rawSize?.toString() ?? '');

    if (value['schemaVersion'] != 1 ||
        value['packageName'] != appPackageName ||
        versionCode == null ||
        versionCode < 1 ||
        versionName.isEmpty) {
      throw const FormatException('Güncelleme sürümü geçersiz.');
    }
    if (apkUri == null ||
        apkUri.scheme != 'https' ||
        apkUri.host.toLowerCase() != gazeteHost ||
        (apkUri.hasPort && apkUri.port != 443) ||
        !apkUri.path.toLowerCase().endsWith('.apk')) {
      throw const FormatException('APK adresi güvenilir değil.');
    }
    if (!RegExp(r'^[a-f0-9]{64}$').hasMatch(sha256)) {
      throw const FormatException('APK doğrulama özeti geçersiz.');
    }
    if (size == null || size < 1 || size > maximumApkBytes) {
      throw const FormatException('APK boyutu geçersiz.');
    }

    return AppUpdateInfo(
      versionCode: versionCode,
      versionName: versionName,
      apkUri: apkUri,
      sha256: sha256,
      size: size,
    );
  }
}

enum InstallRequestStatus { started, permissionRequired }

class InstallRequestResult {
  const InstallRequestResult({required this.status, required this.apkPath});

  final InstallRequestStatus status;
  final String apkPath;
}

class AppUpdateService {
  static const MethodChannel _channel = MethodChannel(
    'com.turnaexpress.gazete/update',
  );

  Future<AppUpdateInfo?> checkForUpdate() async {
    if (!Platform.isAndroid) {
      return null;
    }

    final int currentVersionCode =
        await _channel.invokeMethod<int>('getVersionCode') ?? 0;
    final HttpClient client = HttpClient()
      ..connectionTimeout = const Duration(seconds: 10);

    try {
      final Uri requestUri = apkUpdateManifestUri.replace(
        queryParameters: <String, String>{
          'time': DateTime.now().millisecondsSinceEpoch.toString(),
        },
      );
      final HttpClientRequest request = await client.getUrl(requestUri);
      request.followRedirects = false;
      request.maxRedirects = 0;
      request.headers.set(HttpHeaders.cacheControlHeader, 'no-cache');
      final HttpClientResponse response = await request.close().timeout(
        const Duration(seconds: 15),
      );
      if (response.statusCode != HttpStatus.ok) {
        throw HttpException(
          'Güncelleme bilgisi alınamadı (${response.statusCode}).',
          uri: requestUri,
        );
      }

      if (response.contentLength > _maximumManifestBytes) {
        throw const FormatException('Güncelleme bilgisi çok büyük.');
      }
      final BytesBuilder bodyBytes = BytesBuilder(copy: false);
      int receivedBytes = 0;
      await for (final List<int> chunk in response) {
        receivedBytes += chunk.length;
        if (receivedBytes > _maximumManifestBytes) {
          throw const FormatException('Güncelleme bilgisi çok büyük.');
        }
        bodyBytes.add(chunk);
      }
      final String body = utf8.decode(bodyBytes.takeBytes());
      final AppUpdateInfo info = AppUpdateInfo.fromJson(jsonDecode(body));
      return info.isNewerThan(currentVersionCode) ? info : null;
    } finally {
      client.close(force: true);
    }
  }

  Future<InstallRequestResult> downloadAndInstall(AppUpdateInfo info) async {
    final Map<Object?, Object?>? result = await _channel
        .invokeMapMethod<Object?, Object?>('downloadUpdate', <String, Object>{
          'url': info.apkUri.toString(),
          'sha256': info.sha256,
          'versionCode': info.versionCode,
          'size': info.size,
        });
    return _parseInstallResult(result);
  }

  Future<InstallRequestResult> installDownloaded(
    String apkPath,
    int versionCode,
  ) async {
    final Map<Object?, Object?>? result = await _channel
        .invokeMapMethod<Object?, Object?>(
          'installDownloaded',
          <String, Object>{'path': apkPath, 'versionCode': versionCode},
        );
    return _parseInstallResult(result);
  }

  Future<bool> canInstallPackages() async {
    return await _channel.invokeMethod<bool>('canInstallPackages') ?? false;
  }

  InstallRequestResult _parseInstallResult(Map<Object?, Object?>? result) {
    final String status = result?['status']?.toString() ?? '';
    final String apkPath = result?['path']?.toString() ?? '';
    if (apkPath.isEmpty) {
      throw PlatformException(
        code: 'invalid_update_result',
        message: 'Android güncelleme sonucu geçersiz.',
      );
    }

    return InstallRequestResult(
      status: switch (status) {
        'started' => InstallRequestStatus.started,
        'permission_required' => InstallRequestStatus.permissionRequired,
        _ => throw PlatformException(
          code: 'invalid_update_status',
          message: 'Android güncelleme durumu geçersiz: $status',
        ),
      },
      apkPath: apkPath,
    );
  }
}
