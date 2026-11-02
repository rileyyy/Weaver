import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';

/// A key as the Admin page sees it. The key itself is only ever in the
/// response that issued it (`IssuedApiKey`).
class ApiKey {
  const ApiKey({
    required this.id,
    required this.label,
    required this.scope,
    required this.createdAt,
    this.lastUsedAt,
    this.revokedAt,
  });

  factory ApiKey.fromJson(Map<String, dynamic> json) => ApiKey(
    id: json['id'] as String,
    label: json['label'] as String,
    scope: ApiKeyScope.fromWire(json['scope'] as String),
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
    lastUsedAt: parseOptionalApiTimestamp(json['lastUsedAtUtc']),
    revokedAt: parseOptionalApiTimestamp(json['revokedAtUtc']),
  );

  final String id;
  final String label;
  final ApiKeyScope scope;
  final DateTime createdAt;

  /// The server records use at most about once a minute per key.
  final DateTime? lastUsedAt;

  /// Revoked keys stay listed, so history can still name them.
  final DateTime? revokedAt;

  bool get isRevoked => revokedAt != null;
}
