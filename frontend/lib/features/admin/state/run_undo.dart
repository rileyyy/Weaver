import 'package:weaver/features/admin/models/run_undo_result.dart';

/// Where undoing one run has got to: under way, failed with [error], or
/// done with [result].
class RunUndo {
  const RunUndo.inProgress() : isInProgress = true, error = null, result = null;

  const RunUndo.failed(String this.error) : isInProgress = false, result = null;

  const RunUndo.done(RunUndoResult this.result)
    : isInProgress = false,
      error = null;

  final bool isInProgress;
  final String? error;
  final RunUndoResult? result;
}
