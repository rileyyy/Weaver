import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/board/data/view_repository.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_definition.dart';

@LazySingleton(as: ViewRepository)
class ApiViewRepository implements ViewRepository {
  ApiViewRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  static SavedView _view(Object? json) =>
      SavedView.fromJson(json! as Map<String, dynamic>);

  @override
  Future<List<SavedView>> list() => _api.get(
    '/views',
    (json) => JsonApiClient.listOf(json, SavedView.fromJson),
    failureMessage: 'Failed to load the Views',
  );

  @override
  Future<SavedView> create(ViewDefinition definition) => _api.post(
    '/views',
    definition.toJson(),
    _view,
    failureMessage: 'Failed to save the View',
  );

  @override
  Future<SavedView> update(String id, ViewDefinition definition) => _api.put(
    '/views/$id',
    definition.toJson(),
    _view,
    failureMessage: 'Failed to change the View',
  );

  @override
  Future<void> delete(String id) =>
      _api.delete('/views/$id', failureMessage: 'Failed to delete the View');

  @override
  Future<SavedView> loadDefault() => _api.get(
    '/views/default',
    _view,
    failureMessage: 'Failed to load your default View',
  );

  @override
  Future<SavedView> setDefault(String id) => _api.put(
    '/views/default',
    {'viewId': id},
    _view,
    failureMessage: 'Failed to set your default View',
  );
}
