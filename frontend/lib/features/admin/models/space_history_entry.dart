import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/shared/formatting/day_count.dart';
import 'package:weaver/shared/models/user_kind.dart';

/// One change to a Space, as `GET /spaces/{id}/history` returns it: its
/// creation, a rename or a done-hiding period change.
class SpaceHistoryEntry {
  const SpaceHistoryEntry({
    required this.id,
    required this.changedAt,
    required this.accountName,
    required this.accountKind,
    required this.apiKeyLabel,
    required this.actorLabel,
    required this.field,
    required this.oldValue,
    required this.newValue,
  });

  factory SpaceHistoryEntry.fromJson(Map<String, dynamic> json) {
    final kind = json['accountKind'] as String?;
    return SpaceHistoryEntry(
      id: json['id'] as int,
      changedAt: parseApiTimestamp(json['changedAtUtc'] as String),
      accountName: json['accountName'] as String?,
      accountKind: kind == null ? null : UserKind.fromWire(kind),
      apiKeyLabel: json['apiKeyLabel'] as String?,
      actorLabel: json['actorLabel'] as String?,
      field: json['field'] as String,
      oldValue: json['oldValue'],
      newValue: json['newValue'],
    );
  }

  final int id;
  final DateTime changedAt;

  /// Null when Weaver itself made the change.
  final String? accountName;
  final UserKind? accountKind;
  final String? apiKeyLabel;
  final String? actorLabel;
  final String field;
  final Object? oldValue;
  final Object? newValue;

  String get actorName => accountName ?? 'Weaver';

  bool get isAgent => accountKind == UserKind.agent;

  /// The actor label and key an agent sent, or null when there are neither.
  String? get actorDetail {
    final parts = [?actorLabel, if (apiKeyLabel case final key?) 'key "$key"'];
    return parts.isEmpty ? null : parts.join(' · ');
  }

  /// A one-line description of the change, e.g. "Renamed "Home" → "House"".
  String get change => switch (field) {
    'created' => 'Created "$newValue"',
    'name' => 'Renamed "$oldValue" → "$newValue"',
    'doneHidingDays' =>
      'Done-hiding period: ${_days(oldValue)} → ${_days(newValue)}',
    _ => '$field: ${oldValue ?? 'none'} → ${newValue ?? 'none'}',
  };

  static String _days(Object? value) =>
      value is int ? dayCount(value) : '${value ?? 'none'}';
}
