import 'package:weaver/features/admin/models/run_report.dart';

/// One page of runs, newest first. [nextOffset] is null on the last page.
class RunReportPage {
  const RunReportPage({required this.runs, this.nextOffset});

  factory RunReportPage.fromJson(Map<String, dynamic> json) => RunReportPage(
    runs: [
      for (final run in json['runs'] as List<dynamic>)
        RunReport.fromJson(run as Map<String, dynamic>),
    ],
    nextOffset: json['nextOffset'] as int?,
  );

  final List<RunReport> runs;
  final int? nextOffset;
}
