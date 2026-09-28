import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/user_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/new_user.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';
import 'package:weaver/shared/data/current_user.dart';

/// The Admin page's Users tab. Each change returns null on success, or why
/// it failed for the dialog or snackbar that asked for it.
@injectable
class UserAdminViewModel extends ViewModel {
  UserAdminViewModel(this._repository, this._currentUser);

  final UserAdminRepository _repository;
  final CurrentUser _currentUser;

  List<AdminUser> _users = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  int _loadGeneration = 0;

  List<AdminUser> get users => _users;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  /// The server refuses to deactivate or delete the signed-in account, so
  /// the page doesn't offer it.
  bool isCurrentUser(AdminUser user) => user.id == _currentUser.currentUserId;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final users = await _repository.list();
      if (generation != _loadGeneration) return;
      _users = users;
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the users. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  Future<String?> create(NewUser user) {
    if (user.username.trim().isEmpty) {
      return Future.value('Enter a username.');
    }
    return _attempt(
      () => _repository.create(user),
      onSuccess: (created) => _users = _sorted([..._users, created]),
      fallbackError: 'Could not add the user. Try again.',
    );
  }

  Future<String?> rename(String id, String username) {
    if (username.trim().isEmpty) return Future.value('Enter a username.');
    return _replacing(
      () => _repository.rename(id, username),
      fallbackError: 'Could not rename the user. Try again.',
      resort: true,
    );
  }

  Future<String?> resetPassword(String id, String password) => _replacing(
    () => _repository.resetPassword(id, password),
    fallbackError: 'Could not reset the password. Try again.',
  );

  Future<String?> unlock(String id) => _replacing(
    () => _repository.unlock(id),
    fallbackError: 'Could not unlock the user. Try again.',
  );

  Future<String?> setAdmin(String id, {required bool isAdmin}) => _replacing(
    () => _repository.setAdmin(id, isAdmin: isAdmin),
    fallbackError: 'Could not change admin rights. Try again.',
  );

  Future<String?> setActive(String id, {required bool isActive}) => _replacing(
    () => _repository.setActive(id, isActive: isActive),
    fallbackError: isActive
        ? 'Could not reactivate the user. Try again.'
        : 'Could not deactivate the user. Try again.',
  );

  Future<String?> delete(String id) => _attempt(
    () => _repository.delete(id),
    onSuccess: (_) => _users = [
      for (final user in _users)
        if (user.id != id) user,
    ],
    fallbackError: 'Could not delete the user. Try again.',
  );

  Future<String?> _replacing(
    Future<AdminUser> Function() change, {
    required String fallbackError,
    bool resort = false,
  }) => _attempt(
    change,
    onSuccess: (updated) {
      final replaced = [
        for (final user in _users) user.id == updated.id ? updated : user,
      ];
      _users = resort ? _sorted(replaced) : replaced;
    },
    fallbackError: fallbackError,
  );

  Future<String?> _attempt<R>(
    Future<R> Function() change, {
    required void Function(R result) onSuccess,
    required String fallbackError,
  }) async {
    try {
      onSuccess(await change());
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(e, fallbackError);
    }
  }

  /// The server's order: case-insensitive by username.
  static List<AdminUser> _sorted(List<AdminUser> users) => [...users]
    ..sort(
      (a, b) => a.username.toLowerCase().compareTo(b.username.toLowerCase()),
    );
}
