import 'package:weaver/features/admin/models/space_history_entry.dart';

/// A Space history entry made by a human admin, for tests that only care
/// about the change.
SpaceHistoryEntry spaceHistoryEntry(
  int id,
  String field, {
  Object? oldValue,
  Object? newValue,
}) => SpaceHistoryEntry(
  id: id,
  changedAt: DateTime(2026, 11, 12, 9),
  accountName: 'admin',
  accountKind: null,
  apiKeyLabel: null,
  actorLabel: null,
  field: field,
  oldValue: oldValue,
  newValue: newValue,
);
