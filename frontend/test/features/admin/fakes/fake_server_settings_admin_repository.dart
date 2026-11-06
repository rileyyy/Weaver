import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/server_settings_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_server_settings.dart';

class FakeServerSettingsAdminRepository
    implements ServerSettingsAdminRepository {
  FakeServerSettingsAdminRepository({
    this.timeZoneId = 'UTC',
    this.trashRetentionDays = 30,
  });

  String timeZoneId;
  int trashRetentionDays;
  ApiException? error;
  final List<String> calls = [];

  AdminServerSettings get _settings => AdminServerSettings(
    timeZoneId: timeZoneId,
    trashRetentionDays: trashRetentionDays,
  );

  @override
  Future<AdminServerSettings> get() async {
    calls.add('get');
    final error = this.error;
    if (error != null) throw error;
    return _settings;
  }

  @override
  Future<AdminServerSettings> setTimeZone(String timeZoneId) async {
    calls.add('setTimeZone $timeZoneId');
    final error = this.error;
    if (error != null) throw error;
    this.timeZoneId = timeZoneId;
    return _settings;
  }

  @override
  Future<AdminServerSettings> setTrashRetention(int days) async {
    calls.add('setTrashRetention $days');
    final error = this.error;
    if (error != null) throw error;
    trashRetentionDays = days;
    return _settings;
  }
}
