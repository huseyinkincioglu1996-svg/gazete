import 'package:flutter_test/flutter_test.dart';
import 'package:gazete/navigation_policy.dart';

void main() {
  group('Gazete navigation policy', () {
    test('accepts only the production HTTPS origin in the WebView', () {
      expect(
        isTrustedGazeteUri(
          Uri.parse('https://gazete.turnaexpress.com.tr/menu'),
        ),
        isTrue,
      );
      expect(
        isTrustedGazeteUri(Uri.parse('http://gazete.turnaexpress.com.tr/menu')),
        isFalse,
      );
      expect(
        isTrustedGazeteUri(Uri.parse('https://example.com/menu')),
        isFalse,
      );
      expect(
        isTrustedGazeteUri(
          Uri.parse('https://gazete.turnaexpress.com.tr.example.com/menu'),
        ),
        isFalse,
      );
    });

    test('keeps safe local WebView schemes available', () {
      expect(isSafeLocalWebViewUri(Uri.parse('about:blank')), isTrue);
      expect(
        isSafeLocalWebViewUri(
          Uri.parse('blob:https://gazete.turnaexpress.com.tr/rapor'),
        ),
        isTrue,
      );
      expect(
        isSafeLocalWebViewUri(Uri.parse('data:text/plain,merhaba')),
        isFalse,
      );
      expect(
        isSafeLocalWebViewUri(Uri.parse('blob:https://example.com/rapor')),
        isFalse,
      );
      expect(isSafeLocalWebViewUri(Uri.parse('javascript:alert(1)')), isFalse);
    });

    test('recognizes external app schemes', () {
      expect(canOpenExternally(Uri.parse('tel:+905551112233')), isTrue);
      expect(canOpenExternally(Uri.parse('mailto:test@example.com')), isTrue);
      expect(canOpenExternally(Uri.parse('javascript:alert(1)')), isFalse);
    });

    test('recognizes common downloadable files', () {
      expect(
        isLikelyDownloadUri(Uri.parse('https://example.com/rapor.pdf')),
        isTrue,
      );
      expect(
        isLikelyDownloadUri(Uri.parse('https://example.com/liste.XLSX')),
        isTrue,
      );
      expect(
        isLikelyDownloadUri(Uri.parse('https://example.com/menu')),
        isFalse,
      );
      expect(isLikelyDownloadUri(Uri.parse('javascript:rapor.pdf')), isFalse);
    });
  });
}
