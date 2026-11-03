import 'package:weaver/core/network/api_endpoint.dart';

/// One cached list per Space for a lookup each Space owns (statuses, Types):
/// they only change when an admin edits them, so each Space's is fetched
/// once and shared until [invalidate] is called or the app is pointed at
/// another server. A failed fetch isn't cached.
class SpaceLookupCache<T> {
  SpaceLookupCache(this._endpoint, this._fetch);

  final ApiEndpoint _endpoint;
  final Future<List<T>> Function(String spaceId) _fetch;
  final Map<String, Future<List<T>>> _bySpace = {};
  String? _fetchedFrom;

  Future<List<T>> load(String spaceId) {
    final baseUrl = _endpoint.baseUrl;
    if (baseUrl != _fetchedFrom) {
      _bySpace.clear();
      _fetchedFrom = baseUrl;
    }

    return _bySpace[spaceId] ??= _fetch(spaceId).onError<Object>((
      error,
      stackTrace,
    ) {
      _bySpace.removeWhere((cached, _) => cached == spaceId);
      Error.throwWithStackTrace(error, stackTrace);
    });
  }

  void invalidate() => _bySpace.clear();
}
