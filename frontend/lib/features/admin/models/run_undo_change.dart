import 'package:weaver/features/admin/models/run_undo_outcome.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';

/// One change a run made and what undoing the run did with it. A purged
/// item has no [number] any more.
class RunUndoChange {
  const RunUndoChange({
    required this.title,
    required this.field,
    required this.outcome,
    this.number,
  });

  factory RunUndoChange.fromJson(Map<String, dynamic> json) => RunUndoChange(
    number: json['number'] as int?,
    title: json['title'] as String,
    field: json['field'] as String,
    outcome: RunUndoOutcome.fromJson(json['outcome'] as String),
  );

  final int? number;
  final String title;

  /// The history entry's field, e.g. `startDate` or `link`.
  final String field;
  final RunUndoOutcome outcome;

  /// "#12 Paint the hall", or just the title once the number is gone.
  String get itemName => number == null ? title : '#$number $title';

  String get fieldLabel => WorkItemHistoryEntry.labelFor(field);
}
