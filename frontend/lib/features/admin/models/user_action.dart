import 'package:weaver/features/admin/models/admin_user.dart';

/// What the Users tab's row menu offers.
enum UserAction {
  rename('Rename'),
  resetPassword('Reset password'),
  unlock('Unlock'),
  grantAdmin('Make administrator'),
  revokeAdmin('Remove administrator'),
  deactivate('Deactivate'),
  reactivate('Reactivate'),
  delete('Delete');

  const UserAction(this.label);

  final String label;

  /// The actions that apply to [user] as it is now. The signed-in admin
  /// can't deactivate or delete their own account.
  static List<UserAction> availableFor(
    AdminUser user, {
    required bool isCurrentUser,
  }) => [
    rename,
    resetPassword,
    if (user.isLockedOut) unlock,
    user.isAdmin ? revokeAdmin : grantAdmin,
    if (!user.isActive) reactivate,
    if (user.isActive && !isCurrentUser) deactivate,
    if (!isCurrentUser) delete,
  ];
}
