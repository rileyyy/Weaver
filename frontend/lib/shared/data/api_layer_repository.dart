import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/layer_repository.dart';
import 'package:weaver/shared/models/work_item_layer.dart';

/// Cached on the same terms as `ApiStatusRepository`: layers only change
/// when an admin edits them, and the board needs them on every scope load
/// to colour its cards.
@LazySingleton(as: LayerRepository)
class ApiLayerRepository implements LayerRepository {
  ApiLayerRepository(http.Client client, ApiEndpoint endpoint)
    : _endpoint = endpoint,
      _api = JsonApiClient(client, endpoint);

  final ApiEndpoint _endpoint;
  final JsonApiClient _api;
  Future<List<WorkItemLayer>>? _layers;
  String? _fetchedFrom;

  @override
  Future<List<WorkItemLayer>> loadLayers() {
    final baseUrl = _endpoint.baseUrl;
    if (baseUrl != _fetchedFrom) {
      _layers = null;
      _fetchedFrom = baseUrl;
    }

    return _layers ??= _api
        .get(
          '/work-item-layers',
          (json) => JsonApiClient.listOf(json, WorkItemLayer.fromJson),
          failureMessage: 'Failed to load layers',
        )
        .onError<Object>((error, stackTrace) {
          _layers = null;
          Error.throwWithStackTrace(error, stackTrace);
        });
  }

  @override
  void invalidate() => _layers = null;
}
