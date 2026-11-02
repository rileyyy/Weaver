import 'package:weaver/features/admin/models/admin_server_settings.dart';

/// The server-wide settings. Anyone signed in can read them; each change
/// needs an admin, and the server answers 403 otherwise. Each change returns
/// the settings as they now are.
abstract class ServerSettingsAdminRepository {
  Future<AdminServerSettings> get();

  /// Refused (400) for anything that isn't an IANA time zone id.
  Future<AdminServerSettings> setTimeZone(String timeZoneId);
}
