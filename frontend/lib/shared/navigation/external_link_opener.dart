import 'package:url_launcher/url_launcher.dart';

/// Opens a link outside the app. Widgets that open links take one so tests
/// can pass a fake instead of launching a browser.
typedef LinkOpener = Future<void> Function(Uri uri);

Future<void> openExternalLink(Uri uri) async {
  await launchUrl(uri, mode: LaunchMode.externalApplication);
}
