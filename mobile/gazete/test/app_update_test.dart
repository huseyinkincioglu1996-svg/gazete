import 'package:flutter_test/flutter_test.dart';
import 'package:gazete/app_update.dart';

void main() {
  const String validSha256 =
      '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef';

  test('daha yüksek versionCode güncelleme olarak kabul edilir', () {
    final AppUpdateInfo info = AppUpdateInfo.fromJson(<String, Object>{
      'schemaVersion': 1,
      'packageName': appPackageName,
      'versionCode': 2,
      'versionName': '1.1.0',
      'apkUrl': 'https://gazete.turnaexpress.com.tr/downloads/android/gazete-latest.apk',
      'sha256': validSha256,
      'size': 45_000_000,
    });

    expect(info.isNewerThan(1), isTrue);
    expect(info.isNewerThan(2), isFalse);
    expect(info.isNewerThan(3), isFalse);
  });

  test('güvenilmeyen APK alan adı reddedilir', () {
    expect(
      () => AppUpdateInfo.fromJson(<String, Object>{
        'schemaVersion': 1,
        'packageName': appPackageName,
        'versionCode': 2,
        'versionName': '1.1.0',
        'apkUrl': 'https://example.com/gazete.apk',
        'sha256': validSha256,
        'size': 45_000_000,
      }),
      throwsFormatException,
    );
  });

  test('geçersiz SHA-256 özeti reddedilir', () {
    expect(
      () => AppUpdateInfo.fromJson(<String, Object>{
        'schemaVersion': 1,
        'packageName': appPackageName,
        'versionCode': 2,
        'versionName': '1.1.0',
        'apkUrl': 'https://gazete.turnaexpress.com.tr/downloads/android/gazete-latest.apk',
        'sha256': 'gecersiz',
        'size': 45_000_000,
      }),
      throwsFormatException,
    );
  });

  test('yanlış paket adı ve geçersiz APK boyutu reddedilir', () {
    expect(
      () => AppUpdateInfo.fromJson(<String, Object>{
        'schemaVersion': 1,
        'packageName': 'com.example.sahte',
        'versionCode': 2,
        'versionName': '1.1.0',
        'apkUrl': 'https://gazete.turnaexpress.com.tr/downloads/android/gazete-1.1.0-2.apk',
        'sha256': validSha256,
        'size': 0,
      }),
      throwsFormatException,
    );
  });
}
