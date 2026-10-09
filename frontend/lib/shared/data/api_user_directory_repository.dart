import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/user.dart';

/// Shared by the board and the detail dialog so they don't each fetch
/// `/users`. People can register while the app is open, so the list is
/// re-fetched once it's older than [maxAge], or when the app is pointed at
/// another server. A failed fetch isn't cached.
@LazySingleton(as: UserDirectoryRepository)
class ApiUserDirectoryRepository implements UserDirectoryRepository {
  ApiUserDirectoryRepository(
    http.Client client,
    ApiEndpoint endpoint, {
    @ignoreParam DateTime Function()? now,
  }) : _endpoint = endpoint,
       _api = JsonApiClient(client, endpoint),
       _now = now ?? DateTime.now;

  static const Duration maxAge = Duration(minutes: 5);

  final ApiEndpoint _endpoint;
  final JsonApiClient _api;
  final DateTime Function() _now;
  Future<List<User>>? _users;
  DateTime? _fetchedAt;
  String? _fetchedFrom;

  @override
  Future<List<User>> loadUsers() {
    final baseUrl = _endpoint.baseUrl;
    final fetchedAt = _fetchedAt;
    final cached = _users;
    if (cached != null &&
        fetchedAt != null &&
        baseUrl == _fetchedFrom &&
        _now().difference(fetchedAt) < maxAge) {
      return cached;
    }

    _fetchedAt = _now();
    _fetchedFrom = baseUrl;
    return _users = _api
        .get(
          '/users',
          (json) => JsonApiClient.listOf(json, User.fromJson),
          failureMessage: 'Failed to load users',
        )
        .onError<Object>((error, stackTrace) {
          _users = null;
          _fetchedAt = null;
          Error.throwWithStackTrace(error, stackTrace);
        });
  }

  @override
  void invalidate() {
    _users = null;
    _fetchedAt = null;
  }
}
