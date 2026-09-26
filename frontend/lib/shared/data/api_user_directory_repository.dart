import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/user.dart';

/// Shared by the board and the detail dialog so they don't each fetch
/// `/users`. People can register while the app is open, so the list is
/// re-fetched once it's older than [maxAge]. A failed fetch isn't cached.
@LazySingleton(as: UserDirectoryRepository)
class ApiUserDirectoryRepository implements UserDirectoryRepository {
  ApiUserDirectoryRepository(
    http.Client client,
    @Named('apiBaseUrl') String baseUrl, {
    @ignoreParam DateTime Function()? now,
  }) : _api = JsonApiClient(client, baseUrl),
       _now = now ?? DateTime.now;

  static const Duration maxAge = Duration(minutes: 5);

  final JsonApiClient _api;
  final DateTime Function() _now;
  Future<List<User>>? _users;
  DateTime? _fetchedAt;

  @override
  Future<List<User>> loadUsers() {
    final fetchedAt = _fetchedAt;
    final cached = _users;
    if (cached != null &&
        fetchedAt != null &&
        _now().difference(fetchedAt) < maxAge) {
      return cached;
    }

    _fetchedAt = _now();
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
}
