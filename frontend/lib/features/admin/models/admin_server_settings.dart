/// The server-wide settings an admin edits. Not to be confused with
/// `ServerSettings` in core, which is which server the app talks to.
class AdminServerSettings {
  const AdminServerSettings({required this.timeZoneId});

  factory AdminServerSettings.fromJson(Map<String, dynamic> json) =>
      AdminServerSettings(timeZoneId: json['timeZoneId'] as String);

  /// The IANA time zone, e.g. `Europe/Berlin`, that defines "today" on the
  /// server.
  final String timeZoneId;
}
