import 'package:weaver/core/network/api_dates.dart';

/// An agent account as the Admin page's Agents tab sees it.
class AgentAccount {
  const AgentAccount({
    required this.id,
    required this.name,
    required this.isActive,
    required this.createdAt,
    this.description,
  });

  factory AgentAccount.fromJson(Map<String, dynamic> json) => AgentAccount(
    id: json['id'] as String,
    name: json['name'] as String,
    description: json['description'] as String?,
    isActive: json['isActive'] as bool,
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
  );

  final String id;

  /// Free text, e.g. "Claude Code (desktop)"; not held to username rules.
  final String name;
  final String? description;
  final bool isActive;
  final DateTime createdAt;
}
