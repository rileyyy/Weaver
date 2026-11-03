import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/lookup_admin_repository.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/shared/data/layer_repository.dart';

@LazySingleton(as: LookupAdminRepository)
class ApiLookupAdminRepository implements LookupAdminRepository {
  ApiLookupAdminRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._layerCache,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final LayerRepository _layerCache;

  static LookupValue _value(Object? json) =>
      LookupValue.fromJson(json as Map<String, dynamic>);

  static List<LookupValue> _values(Object? json) =>
      JsonApiClient.listOf(json, LookupValue.fromJson);

  @override
  Future<List<LookupValue>> list(LookupKind kind, {String? spaceId}) =>
      _api.get(
        kind.path,
        _values,
        query: _spaceQuery(kind, spaceId),
        failureMessage: 'Failed to load ${kind.name}',
      );

  @override
  Future<LookupValue> create(
    LookupKind kind,
    String name, {
    String? color,
    String? spaceId,
  }) => _invalidatingCache(
    kind,
    _api.post(
      kind.path,
      {..._details(kind, name, color), ...?_spaceQuery(kind, spaceId)},
      _value,
      failureMessage: 'Failed to add the ${kind.singular}',
    ),
  );

  @override
  Future<LookupValue> update(
    LookupKind kind,
    String id,
    String name, {
    String? color,
  }) => _invalidatingCache(
    kind,
    _api.put(
      '${kind.path}/$id',
      _details(kind, name, color),
      _value,
      failureMessage: 'Failed to save the ${kind.singular}',
    ),
  );

  @override
  Future<List<LookupValue>> reorder(
    LookupKind kind,
    List<String> orderedIds, {
    String? spaceId,
  }) => _invalidatingCache(
    kind,
    _api.put(
      '${kind.path}/order',
      {'orderedIds': orderedIds},
      _values,
      query: _spaceQuery(kind, spaceId),
      failureMessage: 'Failed to reorder the ${kind.name}',
    ),
  );

  @override
  Future<void> delete(LookupKind kind, String id, {String? replacementId}) =>
      _invalidatingCache(
        kind,
        _api.delete(
          '${kind.path}/$id',
          query: replacementId == null
              ? null
              : {'replacementId': replacementId},
          failureMessage: 'Failed to delete the ${kind.singular}',
        ),
      );

  static Map<String, String>? _spaceQuery(LookupKind kind, String? spaceId) {
    if (kind.ownedBySpace != (spaceId != null)) {
      throw ArgumentError.value(
        spaceId,
        'spaceId',
        kind.ownedBySpace
            ? 'Each Space has its own ${kind.name}; say which.'
            : '${kind.name} are shared by every Space.',
      );
    }
    return spaceId == null ? null : {'spaceId': spaceId};
  }

  static Map<String, Object?> _details(
    LookupKind kind,
    String name,
    String? color,
  ) => {'name': name, if (kind.hasColor) 'color': color};

  /// Only layers are cached app-wide (for the board's card colours).
  Future<T> _invalidatingCache<T>(LookupKind kind, Future<T> write) async {
    final result = await write;
    if (kind == LookupKind.layers) _layerCache.invalidate();
    return result;
  }
}
