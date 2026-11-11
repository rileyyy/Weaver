import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/run_report_repository.dart';
import 'package:weaver/features/admin/models/run_changed_item.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/models/run_report_page.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';
import 'package:weaver/shared/models/user_kind.dart';

RunReport runReport(
  String runId, {
  String accountName = 'Runner: llama / When',
  DateTime? at,
  int changeCount = 1,
}) => RunReport(
  accountId: 'agent-1',
  accountName: accountName,
  apiKeyId: 'key-1',
  apiKeyLabel: 'nightly',
  runId: runId,
  firstChangeAt: at ?? DateTime(2026, 11, 10, 2),
  lastChangeAt: at ?? DateTime(2026, 11, 10, 2, 5),
  changeCount: changeCount,
);

RunChangedItem runChangedItem(
  String title, {
  int? number = 12,
  bool isDeleted = false,
  List<WorkItemHistoryEntry>? changes,
}) => RunChangedItem(
  number: number,
  title: title,
  isDeleted: isDeleted,
  changes: changes ?? [runChange('when', null, 'next week')],
);

WorkItemHistoryEntry runChange(
  String field,
  Object? oldValue,
  Object? newValue, {
  String? actorLabel,
}) => WorkItemHistoryEntry(
  id: 1,
  changedAt: DateTime(2026, 11, 10, 2),
  accountName: 'Runner: llama / When',
  accountKind: UserKind.agent,
  apiKeyLabel: 'nightly',
  actorLabel: actorLabel,
  field: field,
  oldValue: oldValue,
  newValue: newValue,
  oldName: null,
  newName: null,
  runId: 'run-1',
);

/// Serves [runs] a page at a time, newest first as given, and each run's
/// changes from [changesByRunId].
class FakeRunReportRepository implements RunReportRepository {
  FakeRunReportRepository(
    this.runs, {
    Map<String, List<RunChangedItem>>? changes,
  }) : changesByRunId = changes ?? {};

  List<RunReport> runs;
  final Map<String, List<RunChangedItem>> changesByRunId;
  ApiException? error;
  final List<String> calls = [];

  @override
  Future<RunReportPage> list({required int offset, required int limit}) async {
    calls.add('list $offset+$limit');
    if (error case final error?) throw error;
    final end = (offset + limit).clamp(0, runs.length);
    return RunReportPage(
      runs: runs.sublist(offset.clamp(0, runs.length), end),
      nextOffset: end < runs.length ? end : null,
    );
  }

  @override
  Future<List<RunChangedItem>> changes(RunReport run) async {
    calls.add('changes ${run.runId}');
    if (error case final error?) throw error;
    return changesByRunId[run.runId] ?? const [];
  }
}
