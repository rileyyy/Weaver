import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_relation.dart';
import 'package:weaver/shared/models/user_kind.dart';

/// One change to a work item, as `GET /work-items/{id}/history` returns it.
/// Values arrive as the JSON the field has in the API; the server also names
/// id values (a status, user or other item) while they still exist, and
/// a custom field (whose changes go by `fields.<id>`) by its current name.
class WorkItemHistoryEntry {
  const WorkItemHistoryEntry({
    required this.id,
    required this.changedAt,
    required this.accountName,
    required this.accountKind,
    required this.apiKeyLabel,
    required this.actorLabel,
    required this.field,
    required this.oldValue,
    required this.newValue,
    required this.oldName,
    required this.newName,
    this.fieldName,
  });

  factory WorkItemHistoryEntry.fromJson(Map<String, dynamic> json) {
    final kind = json['accountKind'] as String?;
    return WorkItemHistoryEntry(
      id: json['id'] as int,
      changedAt: parseApiTimestamp(json['changedAtUtc'] as String),
      accountName: json['accountName'] as String?,
      accountKind: kind == null ? null : UserKind.fromWire(kind),
      apiKeyLabel: json['apiKeyLabel'] as String?,
      actorLabel: json['actorLabel'] as String?,
      field: json['field'] as String,
      oldValue: json['oldValue'],
      newValue: json['newValue'],
      oldName: json['oldName'] as String?,
      newName: json['newName'] as String?,
      fieldName: json['fieldName'] as String?,
    );
  }

  final int id;
  final DateTime changedAt;

  /// Null when Weaver itself made the change, e.g. generating a repeating
  /// item's occurrence.
  final String? accountName;
  final UserKind? accountKind;
  final String? apiKeyLabel;
  final String? actorLabel;
  final String field;
  final Object? oldValue;
  final Object? newValue;
  final String? oldName;
  final String? newName;

  /// A custom field's current name; null for every other field, and for a
  /// custom field that has been removed since.
  final String? fieldName;

  String get actorName => accountName ?? 'Weaver';

  bool get isAgent => accountKind == UserKind.agent;

  /// The actor label and key an agent sent, or null when there are neither.
  String? get actorDetail {
    final parts = [?actorLabel, if (apiKeyLabel case final key?) 'key "$key"'];
    return parts.isEmpty ? null : parts.join(' · ');
  }

  /// A one-line description of the change, e.g. "Status: To Do → Doing".
  String get change {
    final oldText = _text(oldValue, oldName);
    final newText = _text(newValue, newName);
    return switch (field) {
      'created' => 'Created "$newText"',
      'deleted' => 'Deleted "$oldText"',
      'restored' => 'Restored "$newText" from the Trash',
      'purged' => 'Permanently deleted "$oldText" from the Trash',
      'comment' => _verbed('a comment', 'Added', 'Edited', 'Deleted'),
      'link' when oldValue == null => 'Linked${_link(newValue, newName)}',
      'link' => 'Unlinked${_link(oldValue, oldName)}',
      'urlLink' when oldValue == null =>
        'Added URL link ${_urlLink(newValue, newName)}',
      'urlLink' => 'Removed URL link ${_urlLink(oldValue, oldName)}',
      'attachment' when oldValue == null =>
        'Added attachment ${_attachment(newValue, newName)}',
      'attachment' => 'Removed attachment ${_attachment(oldValue, oldName)}',
      // Recorded alongside 'created', saying where the item started.
      'spaceId' when oldValue == null => 'In Space $newText',
      'recurrence' => _verbed(
        '',
        'Started repeating',
        'Repeat',
        'Stopped repeating',
      ),
      _ => '$_fieldLabel: $oldText → $newText',
    };
  }

  String _verbed(String noun, String added, String edited, String removed) {
    final subject = noun.isEmpty ? '' : ' $noun';
    final oldText = _text(oldValue, oldName);
    final newText = _text(newValue, newName);
    if (oldValue == null) return '$added$subject: $newText';
    if (newValue == null) return '$removed$subject: $oldText';
    return '$edited$subject: $oldText → $newText';
  }

  /// Typed links record `{ workItemId, type }`, with the type seen from
  /// this item; rows from before link types hold just the other item's id.
  /// The name is absent once the other item is purged.
  static String _link(Object? value, String? name) {
    if (value is! Map) return ' ${_text(value, name)}';
    final other = name ?? '${value['workItemId']}';
    final type = switch (value['type']) {
      final String wire => WorkItemLinkRelation.tryFromWire(wire),
      _ => null,
    };
    return type == null ? ' $other' : ': ${type.phrase} $other';
  }

  static String _urlLink(Object? value, String? name) {
    if (name != null) return name;
    if (value is! Map) return _text(value, null);
    return '${value['label'] ?? value['url']}';
  }

  /// Attachments record `{ id, fileName }`.
  static String _attachment(Object? value, String? name) {
    if (name != null) return name;
    if (value is! Map) return _text(value, null);
    return '${value['fileName']}';
  }

  /// Mirrors the backend's `WorkItemField.CustomFieldPrefix`.
  static const String customFieldPrefix = 'fields.';

  bool get isCustomField => field.startsWith(customFieldPrefix);

  String get _fieldLabel =>
      fieldName ??
      (isCustomField ? 'Removed field' : _fieldLabels[field] ?? field);

  static const _fieldLabels = {
    'title': 'Title',
    'description': 'Description',
    'typeId': 'Type',
    'priorityId': 'Priority',
    'when': 'When',
    'statusId': 'Status',
    'parentId': 'Parent',
    'spaceId': 'Space',
    'startDate': 'Start date',
    'endDate': 'End date',
    'assignedToUserId': 'Assignee',
    'tags': 'Tags',
  };

  static String _text(Object? value, String? name) => switch (value) {
    _ when name != null => name,
    null => 'none',
    List(isEmpty: true) => 'none',
    List() => value.join(', '),
    Map() => _schedule(value),
    _ => '$value',
  };

  static String _schedule(Map<dynamic, dynamic> schedule) {
    final days = (schedule['daysOfWeek'] as List<dynamic>?) ?? const [];
    final end = schedule['endDate'];
    return [
      schedule['frequency'],
      if (days.isNotEmpty) 'on ${days.join(', ')}',
      'from ${schedule['startDate']}',
      if (end != null) 'until $end',
    ].join(' ');
  }
}
