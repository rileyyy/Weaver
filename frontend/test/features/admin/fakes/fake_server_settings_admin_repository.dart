import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/server_settings_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_server_settings.dart';

class FakeServerSettingsAdminRepository
    implements ServerSettingsAdminRepository {
  FakeServerSettingsAdminRepository({this.timeZoneId = 'UTC'});

  String timeZoneId;
  ApiException? error;
  final List<String> calls = [];

  @override
  Future<AdminServerSettings> get() async {
    calls.add('get');
    final error = this.error;
    if (error != null) throw error;
    return AdminServerSettings(timeZoneId: timeZoneId);
  }

  @override
  Future<AdminServerSettings> setTimeZone(String timeZoneId) async {
    calls.add('setTimeZone $timeZoneId');
    final error = this.error;
    if (error != null) throw error;
    this.timeZoneId = timeZoneId;
    return AdminServerSettings(timeZoneId: timeZoneId);
  }
}
