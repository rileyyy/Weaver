import 'package:weaver/features/admin/models/run_changed_item.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/models/run_report_page.dart';
import 'package:weaver/features/admin/models/run_undo_result.dart';

/// Agent run reports. Every call needs an admin; the server answers 403
/// otherwise.
abstract class RunReportRepository {
  /// Newest first, in pages the server sizes.
  Future<RunReportPage> list({required int offset});

  /// The items [run] changed, in the order it first changed each.
  Future<List<RunChangedItem>> changes(RunReport run);

  /// Undoes what [run] did, except what someone has changed since, and says
  /// what happened to each change. Comments are kept.
  Future<RunUndoResult> undo(RunReport run);
}
