import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/shared/models/user_kind.dart';

/// A user as the Admin page sees it, including deactivated accounts.
class AdminUser {
  const AdminUser({
    required this.id,
    required this.username,
    required this.kind,
    required this.isAdmin,
    required this.isActive,
    this.lockedUntil,
  });

  factory AdminUser.fromJson(Map<String, dynamic> json) => AdminUser(
    id: json['id'] as String,
    username: json['username'] as String,
    kind: UserKind.fromWire(json['kind'] as String),
    isAdmin: json['isAdmin'] as bool,
    isActive: json['isActive'] as bool,
    lockedUntil: parseOptionalApiTimestamp(json['lockedUntilUtc']),
  );

  final String id;
  final String username;
  final UserKind kind;
  final bool isAdmin;
  final bool isActive;

  /// Local time; only set while a lockout is still in force.
  final DateTime? lockedUntil;

  bool get isLockedOut => lockedUntil != null;
}
