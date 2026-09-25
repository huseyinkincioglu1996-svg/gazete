const String gazeteHost = 'gazete.turnaexpress.com.tr';
final Uri gazeteStartUri = Uri.https(gazeteHost, '/menu');

const Set<String> _externalSchemes = <String>{
  'geo',
  'http',
  'https',
  'mailto',
  'sms',
  'tel',
};

const Set<String> _downloadExtensions = <String>{
  'apk',
  'csv',
  'doc',
  'docx',
  'pdf',
  'xls',
  'xlsx',
  'zip',
};

bool isTrustedGazeteUri(Uri uri) {
  return uri.scheme == 'https' && uri.host.toLowerCase() == gazeteHost;
}

bool isSafeLocalWebViewUri(Uri uri) {
  if (uri.toString() == 'about:blank') {
    return true;
  }
  if (uri.scheme != 'blob') {
    return false;
  }
  final Uri? blobOrigin = Uri.tryParse(uri.path);
  return blobOrigin != null && isTrustedGazeteUri(blobOrigin);
}

bool canOpenExternally(Uri uri) {
  return _externalSchemes.contains(uri.scheme.toLowerCase());
}

bool isLikelyDownloadUri(Uri uri) {
  if (uri.scheme != 'http' && uri.scheme != 'https') {
    return false;
  }
  if (uri.pathSegments.isEmpty) {
    return false;
  }
  final String fileName = uri.pathSegments.last.toLowerCase();
  final int extensionSeparator = fileName.lastIndexOf('.');
  if (extensionSeparator == -1 || extensionSeparator == fileName.length - 1) {
    return false;
  }
  return _downloadExtensions.contains(
    fileName.substring(extensionSeparator + 1),
  );
}
