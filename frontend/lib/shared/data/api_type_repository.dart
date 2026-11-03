import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/type_repository.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// Cached on the same terms as `ApiStatusRepository`: types only change
/// when an admin edits them, and the board needs them on every scope load
/// to colour its cards.
@LazySingleton(as: TypeRepository)
class ApiTypeRepository implements TypeRepository {
  ApiTypeRepository(http.Client client, ApiEndpoint endpoint)
    : _endpoint = endpoint,
      _api = JsonApiClient(client, endpoint);

  final ApiEndpoint _endpoint;
  final JsonApiClient _api;
  Future<List<WorkItemType>>? _types;
  String? _fetchedFrom;

  @override
  Future<List<WorkItemType>> loadTypes() {
    final baseUrl = _endpoint.baseUrl;
    if (baseUrl != _fetchedFrom) {
      _types = null;
      _fetchedFrom = baseUrl;
    }

    return _types ??= _api
        .get(
          '/work-item-types',
          (json) => JsonApiClient.listOf(json, WorkItemType.fromJson),
          failureMessage: 'Failed to load types',
        )
        .onError<Object>((error, stackTrace) {
          _types = null;
          Error.throwWithStackTrace(error, stackTrace);
        });
  }

  @override
  void invalidate() => _types = null;
}
