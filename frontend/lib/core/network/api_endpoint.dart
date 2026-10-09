/// Where the API lives. Repositories read it on every request instead of
/// capturing it once, because a native build learns the server at sign-in,
/// after the repositories already exist.
abstract class ApiEndpoint {
  /// The API root, ending in `/api` with no trailing slash.
  String get baseUrl;
}
