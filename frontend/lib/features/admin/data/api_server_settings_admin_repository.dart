import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/server_settings_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_server_settings.dart';

@LazySingleton(as: ServerSettingsAdminRepository)
class ApiServerSettingsAdminRepository
    implements ServerSettingsAdminRepository {
  ApiServerSettingsAdminRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  static AdminServerSettings _settings(Object? json) =>
      AdminServerSettings.fromJson(json as Map<String, dynamic>);

  @override
  Future<AdminServerSettings> get() => _api.get(
    '/server-settings',
    _settings,
    failureMessage: 'Failed to load the server settings',
  );

  @override
  Future<AdminServerSettings> setTimeZone(String timeZoneId) => _api.put(
    '/server-settings/time-zone',
    {'timeZoneId': timeZoneId},
    _settings,
    failureMessage: 'Failed to change the time zone',
  );
}
