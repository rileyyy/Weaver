/// What undoing a run did with one of its changes. Mirrors the backend's
/// `RunUndoOutcome`.
enum RunUndoOutcome {
  undone,
  kept,
  changedSince,
  itemGone,
  invalidDates,
  notUndoable;

  /// An unknown name from a newer server reads as not undoable, the
  /// outcome that promises least.
  static RunUndoOutcome fromJson(String name) => switch (name) {
    'Undone' => undone,
    'Kept' => kept,
    'ChangedSince' => changedSince,
    'ItemGone' => itemGone,
    'InvalidDates' => invalidDates,
    _ => notUndoable,
  };

  /// Why a change wasn't put back; null when it was, or was meant to stay.
  String? get skipReason => switch (this) {
    undone || kept => null,
    changedSince => 'changed by someone since',
    itemGone => 'the item is in the Trash',
    invalidDates => 'would start after its end date',
    notUndoable => 'can’t be undone automatically',
  };
}
