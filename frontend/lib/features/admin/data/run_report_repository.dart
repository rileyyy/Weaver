import 'package:weaver/features/admin/models/run_changed_item.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/models/run_report_page.dart';

/// Agent run reports. Every call needs an admin; the server answers 403
/// otherwise.
abstract class RunReportRepository {
  /// Newest first.
  Future<RunReportPage> list({required int offset, required int limit});

  /// The items [run] changed, in the order it first changed each.
  Future<List<RunChangedItem>> changes(RunReport run);
}
