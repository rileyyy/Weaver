import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/features/admin/models/run_key.dart';

/// One agent run as the Admin page's Runs tab lists it: the account and key
/// that made it, its run id, and its first and last changes.
class RunReport {
  const RunReport({
    required this.accountId,
    required this.accountName,
    required this.runId,
    required this.firstChangeAt,
    required this.lastChangeAt,
    required this.apiKeyId,
    required this.apiKeyLabel,
    required this.changeCount,
  });

  factory RunReport.fromJson(Map<String, dynamic> json) => RunReport(
    accountId: json['accountId'] as String,
    accountName: json['accountName'] as String,
    apiKeyId: json['apiKeyId'] as String,
    apiKeyLabel: json['apiKeyLabel'] as String,
    runId: json['runId'] as String,
    firstChangeAt: parseApiTimestamp(json['firstChangeAtUtc'] as String),
    lastChangeAt: parseApiTimestamp(json['lastChangeAtUtc'] as String),
    changeCount: json['changeCount'] as int,
  );

  final String accountId;
  final String accountName;
  final String apiKeyId;
  final String apiKeyLabel;

  /// Free text the agent chose; only unique together with its account and key.
  final String runId;
  final DateTime firstChangeAt;
  final DateTime lastChangeAt;
  final int changeCount;

  RunKey get key =>
      RunKey(accountId: accountId, apiKeyId: apiKeyId, runId: runId);
}
