/// What tells one run from another, as the server's `RunKey` does: the run
/// id, and the agent account and key that sent it. Run ids are free text, so
/// two agents may send the same one; they are still two runs.
class RunKey {
  const RunKey({
    required this.accountId,
    required this.apiKeyId,
    required this.runId,
  });

  final String accountId;
  final String apiKeyId;
  final String runId;

  @override
  bool operator ==(Object other) =>
      other is RunKey &&
      other.accountId == accountId &&
      other.apiKeyId == apiKeyId &&
      other.runId == runId;

  @override
  int get hashCode => Object.hash(accountId, apiKeyId, runId);
}
