/// The server-wide settings an admin edits. Not to be confused with
/// `ServerSettings` in core, which is which server the app talks to.
class AdminServerSettings {
  const AdminServerSettings({
    required this.timeZoneId,
    required this.trashRetentionDays,
  });

  factory AdminServerSettings.fromJson(Map<String, dynamic> json) =>
      AdminServerSettings(
        timeZoneId: json['timeZoneId'] as String,
        trashRetentionDays: json['trashRetentionDays'] as int,
      );

  /// The IANA time zone, e.g. `Europe/Berlin`, that defines "today" on the
  /// server.
  final String timeZoneId;

  /// How long an item stays in the Trash before it is purged (ADR-0007).
  final int trashRetentionDays;

  /// The range the server accepts for [trashRetentionDays].
  static const int minTrashRetentionDays = 1;
  static const int maxTrashRetentionDays = 3650;
}
