/// Turns a typed server address into the origin the API lives under, or
/// null if it isn't a usable http(s) address.
///
/// A missing scheme means https, since that's how Tailscale (and anything
/// else reachable beyond a dev machine) serves Weaver. A pasted `/api`
/// suffix is dropped because the client appends it itself.
String? normalizeServerOrigin(String input) {
  final text = input.trim();
  if (text.isEmpty) return null;

  final uri = Uri.tryParse(text.contains('://') ? text : 'https://$text');
  if (uri == null || !_isServerAddress(uri)) return null;

  final path = uri.path
      .replaceFirst(RegExp(r'/+$'), '')
      .replaceFirst(RegExp(r'/api$'), '');
  return uri.replace(path: path).toString();
}

bool _isServerAddress(Uri uri) =>
    (uri.isScheme('http') || uri.isScheme('https')) &&
    uri.host.isNotEmpty &&
    uri.userInfo.isEmpty &&
    !uri.hasQuery &&
    !uri.hasFragment;
