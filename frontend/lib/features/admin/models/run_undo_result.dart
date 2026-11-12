import 'package:weaver/features/admin/models/run_undo_change.dart';
import 'package:weaver/features/admin/models/run_undo_outcome.dart';

/// Every change an undone run made, in order, each with what the undo did.
class RunUndoResult {
  const RunUndoResult(this.changes);

  factory RunUndoResult.fromJson(Map<String, dynamic> json) => RunUndoResult([
    for (final change in json['changes'] as List<dynamic>)
      RunUndoChange.fromJson(change as Map<String, dynamic>),
  ]);

  final List<RunUndoChange> changes;

  int get undoneCount => _count(RunUndoOutcome.undone);

  int get keptCount => _count(RunUndoOutcome.kept);

  /// The changes left as they are, each with a reason.
  List<RunUndoChange> get skipped =>
      changes.where((c) => c.outcome.skipReason != null).toList();

  int _count(RunUndoOutcome outcome) =>
      changes.where((c) => c.outcome == outcome).length;
}
