import 'dart:async';

import 'package:android_file_picker/android_file_picker.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:webview_flutter/webview_flutter.dart';
import 'package:webview_flutter_android/webview_flutter_android.dart';

import 'app_update.dart';
import 'navigation_policy.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setSystemUIOverlayStyle(
    const SystemUiOverlayStyle(
      statusBarColor: Color(0xFF050506),
      statusBarIconBrightness: Brightness.light,
      systemNavigationBarColor: Color(0xFF050506),
      systemNavigationBarIconBrightness: Brightness.light,
    ),
  );
  runApp(const GazeteApp());
}

class GazeteApp extends StatelessWidget {
  const GazeteApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Gazete',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFFA9003B),
          brightness: Brightness.dark,
        ),
        scaffoldBackgroundColor: const Color(0xFF050506),
        useMaterial3: true,
      ),
      home: const GazeteWebViewPage(),
    );
  }
}

class GazeteWebViewPage extends StatefulWidget {
  const GazeteWebViewPage({super.key});

  @override
  State<GazeteWebViewPage> createState() => _GazeteWebViewPageState();
}

class _GazeteWebViewPageState extends State<GazeteWebViewPage>
    with WidgetsBindingObserver {
  static const double _edgeGestureWidth = 32;
  static const double _backGestureDistance = 96;

  late final WebViewController _controller;
  int _loadingProgress = 0;
  bool _mainFrameFailed = false;
  double? _edgeGestureStart;
  bool _edgeGestureHandled = false;
  final AppUpdateService _updateService = AppUpdateService();
  AppUpdateInfo? _availableUpdate;
  bool _isCheckingForUpdate = false;
  bool _isUpdating = false;
  bool _awaitingInstallPermission = false;
  String? _downloadedApkPath;
  int? _downloadedVersionCode;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);

    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setBackgroundColor(const Color(0xFF050506))
      ..setNavigationDelegate(
        NavigationDelegate(
          onNavigationRequest: _handleNavigation,
          onPageStarted: (_) {
            if (!mounted) {
              return;
            }
            setState(() {
              _loadingProgress = 0;
              _mainFrameFailed = false;
            });
          },
          onProgress: (int progress) {
            if (!mounted) {
              return;
            }
            setState(() => _loadingProgress = progress);
          },
          onPageFinished: (_) {
            if (!mounted) {
              return;
            }
            setState(() => _loadingProgress = 100);
          },
          onWebResourceError: (WebResourceError error) {
            if (error.isForMainFrame != true || !mounted) {
              return;
            }
            setState(() {
              _loadingProgress = 100;
              _mainFrameFailed = true;
            });
          },
          onSslAuthError: (SslAuthError error) {
            error.cancel();
            if (mounted) {
              setState(() {
                _loadingProgress = 100;
                _mainFrameFailed = true;
              });
            }
          },
        ),
      );

    final platform = _controller.platform;
    if (platform is AndroidWebViewController) {
      unawaited(_configureAndroidWebViewAndLoad(platform));
    } else {
      unawaited(_controller.loadRequest(gazeteStartUri));
    }

    unawaited(_checkForUpdate());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state != AppLifecycleState.resumed || _isUpdating) {
      return;
    }

    if (_awaitingInstallPermission && _downloadedApkPath != null) {
      unawaited(_resumePendingInstall());
      return;
    }

    unawaited(_checkForUpdate());
  }

  Future<void> _configureAndroidWebViewAndLoad(
    AndroidWebViewController controller,
  ) async {
    await AndroidWebViewController.enableDebugging(!kReleaseMode);
    await controller.setMediaPlaybackRequiresUserGesture(true);
    await controller.setGeolocationEnabled(true);
    await controller.setAllowContentAccess(true);
    await controller.setOnShowFileSelector(_selectFiles);
    await controller.setGeolocationPermissionsPromptCallbacks(
      onShowPrompt: (GeolocationPermissionsRequestParams request) async {
        final Uri? origin = Uri.tryParse(request.origin);
        if (origin == null || !isTrustedGazeteUri(origin)) {
          return const GeolocationPermissionsResponse(
            allow: false,
            retain: false,
          );
        }

        PermissionStatus status = await Permission.locationWhenInUse.status;
        if (!status.isGranted) {
          status = await Permission.locationWhenInUse.request();
        }

        return GeolocationPermissionsResponse(
          allow: status.isGranted,
          retain: status.isGranted,
        );
      },
    );
    await _controller.loadRequest(gazeteStartUri);
  }

  Future<List<String>> _selectFiles(FileSelectorParams params) async {
    if (params.mode == FileSelectorMode.save) {
      return const <String>[];
    }

    try {
      final bool imagesOnly =
          params.acceptTypes.isNotEmpty &&
          params.acceptTypes.every(
            (String type) => type == 'image/*' || type.startsWith('image/'),
          );

      final FileType type = imagesOnly ? FileType.image : FileType.any;
      const FilePickerAndroidOptions androidOptions = FilePickerAndroidOptions(
        safOptions: AndroidSAFOptions(
          grant: AndroidSAFGrant.transient,
          accessMode: AndroidSAFAccessMode.readOnly,
          persistGrant: false,
        ),
      );
      final List<PlatformFile> files;
      if (params.mode == FileSelectorMode.openMultiple) {
        files = await FilePicker.pickFiles(
          type: type,
          androidOptions: androidOptions,
        );
      } else {
        final PlatformFile? file = await FilePicker.pickFile(
          type: type,
          androidOptions: androidOptions,
        );
        files = file == null ? const <PlatformFile>[] : <PlatformFile>[file];
      }

      return files.map(_fileUriForWebView).toList(growable: false);
    } on Exception {
      _showMessage('Dosya seçici açılamadı. Lütfen yeniden deneyin.');
      return const <String>[];
    }
  }

  String _fileUriForWebView(PlatformFile file) {
    if (file is AndroidPlatformFile && file.safHandle != null) {
      return file.safHandle!.uri.toString();
    }
    return file.uri.toString();
  }

  Future<NavigationDecision> _handleNavigation(
    NavigationRequest request,
  ) async {
    final Uri? uri = Uri.tryParse(request.url);
    if (uri == null) {
      return NavigationDecision.prevent;
    }

    if (isTrustedGazeteUri(uri) || isSafeLocalWebViewUri(uri)) {
      if (!isLikelyDownloadUri(uri)) {
        return NavigationDecision.navigate;
      }
    }

    if (uri.scheme == 'http' && uri.host == gazeteHost) {
      await _controller.loadRequest(uri.replace(scheme: 'https'));
      return NavigationDecision.prevent;
    }

    if (canOpenExternally(uri) || isLikelyDownloadUri(uri)) {
      try {
        final bool opened = await launchUrl(
          uri,
          mode: LaunchMode.externalApplication,
        );
        if (!opened) {
          _showMessage('Bağlantı açılamadı.');
        }
      } on Exception {
        _showMessage('Bağlantıyı açacak bir uygulama bulunamadı.');
      }
    } else {
      _showMessage('Güvenli olmayan bağlantı engellendi.');
    }

    return NavigationDecision.prevent;
  }

  Future<void> _checkForUpdate() async {
    if (_isCheckingForUpdate || _isUpdating) {
      return;
    }

    _isCheckingForUpdate = true;
    try {
      final AppUpdateInfo? update = await _updateService.checkForUpdate();
      if (!mounted) {
        return;
      }
      setState(() => _availableUpdate = update);
    } on Exception {
      // Güncelleme kontrolü uygulamanın normal kullanımını engellememelidir.
    } finally {
      _isCheckingForUpdate = false;
    }
  }

  Future<void> _startUpdate() async {
    final AppUpdateInfo? update = _availableUpdate;
    if (update == null || _isUpdating) {
      return;
    }
    if (_downloadedApkPath != null &&
        _downloadedVersionCode == update.versionCode) {
      await _installDownloadedUpdate();
      return;
    }

    setState(() => _isUpdating = true);
    try {
      final InstallRequestResult result = await _updateService
          .downloadAndInstall(update);
      _downloadedApkPath = result.apkPath;
      _downloadedVersionCode = update.versionCode;
      _handleInstallResult(result);
    } on Exception {
      _showMessage('Güncelleme indirilemedi. Lütfen yeniden deneyin.');
    } finally {
      if (mounted) {
        setState(() => _isUpdating = false);
      }
    }
  }

  Future<void> _installDownloadedUpdate() async {
    final String? apkPath = _downloadedApkPath;
    final int? versionCode = _downloadedVersionCode;
    if (apkPath == null || versionCode == null || _isUpdating) {
      return;
    }

    setState(() => _isUpdating = true);
    try {
      final InstallRequestResult result = await _updateService
          .installDownloaded(apkPath, versionCode);
      _handleInstallResult(result);
    } on Exception {
      _awaitingInstallPermission = false;
      _downloadedApkPath = null;
      _downloadedVersionCode = null;
      _showMessage('Android kurulum ekranı açılamadı.');
    } finally {
      if (mounted) {
        setState(() => _isUpdating = false);
      }
    }
  }

  Future<void> _resumePendingInstall() async {
    if (_isUpdating) {
      return;
    }

    try {
      final bool canInstall = await _updateService.canInstallPackages();
      if (!mounted) {
        return;
      }
      if (!canInstall) {
        _awaitingInstallPermission = false;
        _showMessage(
          'Kurulum izni verilmedi. Güncellemek için butona yeniden dokunun.',
        );
        return;
      }
      await _installDownloadedUpdate();
    } on Exception {
      _awaitingInstallPermission = false;
      _showMessage('Kurulum izni kontrol edilemedi.');
    }
  }

  void _handleInstallResult(InstallRequestResult result) {
    _awaitingInstallPermission =
        result.status == InstallRequestStatus.permissionRequired;
    if (_awaitingInstallPermission) {
      _showMessage('Bu kaynaktan kuruluma izin verip uygulamaya geri dönün.');
    }
  }

  void _showMessage(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _goBack() async {
    if (await _controller.canGoBack()) {
      await _controller.goBack();
      return;
    }
    await SystemNavigator.pop();
  }

  void _handlePointerDown(PointerDownEvent event) {
    if (event.position.dx <= _edgeGestureWidth) {
      _edgeGestureStart = event.position.dx;
      _edgeGestureHandled = false;
    }
  }

  void _handlePointerMove(PointerMoveEvent event) {
    final double? start = _edgeGestureStart;
    if (start == null || _edgeGestureHandled) {
      return;
    }
    if (event.position.dx - start >= _backGestureDistance) {
      _edgeGestureHandled = true;
      unawaited(_goBack());
    }
  }

  void _resetPointerGesture(PointerEvent _) {
    _edgeGestureStart = null;
    _edgeGestureHandled = false;
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (bool didPop, Object? result) {
        if (!didPop) {
          unawaited(_goBack());
        }
      },
      child: Scaffold(
        body: SafeArea(
          child: Listener(
            behavior: HitTestBehavior.translucent,
            onPointerDown: _handlePointerDown,
            onPointerMove: _handlePointerMove,
            onPointerUp: _resetPointerGesture,
            onPointerCancel: _resetPointerGesture,
            child: Stack(
              children: <Widget>[
                Positioned.fill(child: WebViewWidget(controller: _controller)),
                if (_loadingProgress < 100 && !_mainFrameFailed)
                  Align(
                    alignment: Alignment.topCenter,
                    child: LinearProgressIndicator(
                      minHeight: 3,
                      value: _loadingProgress == 0
                          ? null
                          : _loadingProgress / 100,
                      color: const Color(0xFFE4004F),
                      backgroundColor: const Color(0xFF202024),
                    ),
                  ),
                if (_mainFrameFailed)
                  Positioned.fill(
                    child: ColoredBox(
                      color: const Color(0xFF050506),
                      child: Center(
                        child: Padding(
                          padding: const EdgeInsets.all(32),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: <Widget>[
                              const Icon(
                                Icons.wifi_off_rounded,
                                size: 64,
                                color: Color(0xFFE4004F),
                              ),
                              const SizedBox(height: 20),
                              Text(
                                'Sayfaya ulaşılamıyor',
                                style: Theme.of(context).textTheme.headlineSmall
                                    ?.copyWith(fontWeight: FontWeight.w800),
                                textAlign: TextAlign.center,
                              ),
                              const SizedBox(height: 10),
                              const Text(
                                'İnternet bağlantınızı kontrol edip yeniden deneyin.',
                                textAlign: TextAlign.center,
                                style: TextStyle(color: Color(0xFFB8B8BE)),
                              ),
                              const SizedBox(height: 24),
                              FilledButton.icon(
                                onPressed: () {
                                  setState(() {
                                    _mainFrameFailed = false;
                                    _loadingProgress = 0;
                                  });
                                  unawaited(_controller.reload());
                                },
                                icon: const Icon(Icons.refresh_rounded),
                                label: const Text('Tekrar dene'),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                if (_availableUpdate != null)
                  Positioned(
                    top: 12,
                    right: 12,
                    child: FilledButton.icon(
                      onPressed: _isUpdating ? null : _startUpdate,
                      style: FilledButton.styleFrom(
                        backgroundColor: const Color(0xFFE4004F),
                        foregroundColor: Colors.white,
                        disabledBackgroundColor: const Color(0xFF7A1738),
                        disabledForegroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 14,
                          vertical: 11,
                        ),
                        elevation: 8,
                      ),
                      icon: _isUpdating
                          ? const SizedBox.square(
                              dimension: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.2,
                                color: Colors.white,
                              ),
                            )
                          : const Icon(Icons.system_update_alt_rounded),
                      label: Text(
                        _isUpdating ? 'İndiriliyor...' : 'Güncelle',
                        style: const TextStyle(fontWeight: FontWeight.w800),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
