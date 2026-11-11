import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/board/data/calendar_repository.dart';

@LazySingleton(as: CalendarRepository)
class ApiCalendarRepository implements CalendarRepository {
  ApiCalendarRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  @override
  Future<DateTime> today() => _api.get(
    '/calendar/today',
    (json) => parseCalendarDate((json as Map<String, dynamic>)['today'])!,
    failureMessage: "Failed to load today's date",
  );
}
