import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/user_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/new_user.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';

/// Every successful change drops the shared user directory cache, so
/// assignee pickers and names pick it up next time they load.
@LazySingleton(as: UserAdminRepository)
class ApiUserAdminRepository implements UserAdminRepository {
  ApiUserAdminRepository(
    http.Client client,
    @Named('apiBaseUrl') String baseUrl,
    this._directory,
  ) : _api = JsonApiClient(client, baseUrl);

  final JsonApiClient _api;
  final UserDirectoryRepository _directory;

  static AdminUser _user(Object? json) =>
      AdminUser.fromJson(json as Map<String, dynamic>);

  @override
  Future<List<AdminUser>> list() => _api.get(
    '/admin/users',
    (json) => JsonApiClient.listOf(json, AdminUser.fromJson),
    failureMessage: 'Failed to load users',
  );

  @override
  Future<AdminUser> create(NewUser user) => _invalidatingDirectory(
    _api.post(
      '/admin/users',
      user.toJson(),
      _user,
      failureMessage: 'Failed to add the user',
    ),
  );

  @override
  Future<AdminUser> rename(String id, String username) => _put(id, 'username', {
    'username': username,
  }, failureMessage: 'Failed to rename the user');

  @override
  Future<AdminUser> resetPassword(String id, String password) => _put(
    id,
    'password',
    {'password': password},
    failureMessage: 'Failed to reset the password',
  );

  @override
  Future<AdminUser> unlock(String id) => _invalidatingDirectory(
    _api.deleteReturning(
      '/admin/users/$id/lockout',
      _user,
      failureMessage: 'Failed to unlock the user',
    ),
  );

  @override
  Future<AdminUser> setAdmin(String id, {required bool isAdmin}) => _put(
    id,
    'admin',
    {'isAdmin': isAdmin},
    failureMessage: 'Failed to change admin rights',
  );

  @override
  Future<AdminUser> setActive(String id, {required bool isActive}) => _put(
    id,
    'active',
    {'isActive': isActive},
    failureMessage: isActive
        ? 'Failed to reactivate the user'
        : 'Failed to deactivate the user',
  );

  @override
  Future<void> delete(String id) => _invalidatingDirectory(
    _api.delete(
      '/admin/users/$id',
      failureMessage: 'Failed to delete the user',
    ),
  );

  Future<AdminUser> _put(
    String id,
    String field,
    Map<String, dynamic> body, {
    required String failureMessage,
  }) => _invalidatingDirectory(
    _api.put(
      '/admin/users/$id/$field',
      body,
      _user,
      failureMessage: failureMessage,
    ),
  );

  Future<T> _invalidatingDirectory<T>(Future<T> write) async {
    final result = await write;
    _directory.invalidate();
    return result;
  }
}
