import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/lookup_admin_repository.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';

@LazySingleton(as: LookupAdminRepository)
class ApiLookupAdminRepository implements LookupAdminRepository {
  ApiLookupAdminRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  static LookupValue _value(Object? json) =>
      LookupValue.fromJson(json as Map<String, dynamic>);

  static List<LookupValue> _values(Object? json) =>
      JsonApiClient.listOf(json, LookupValue.fromJson);

  @override
  Future<List<LookupValue>> list(LookupKind kind) => _api.get(
    kind.path,
    _values,
    failureMessage: 'Failed to load ${kind.name}',
  );

  @override
  Future<LookupValue> create(LookupKind kind, String name) => _api.post(
    kind.path,
    {'name': name},
    _value,
    failureMessage: 'Failed to add the ${kind.singular}',
  );

  @override
  Future<LookupValue> rename(LookupKind kind, String id, String name) =>
      _api.put(
        '${kind.path}/$id',
        {'name': name},
        _value,
        failureMessage: 'Failed to rename the ${kind.singular}',
      );

  @override
  Future<List<LookupValue>> reorder(LookupKind kind, List<String> orderedIds) =>
      _api.put(
        '${kind.path}/order',
        {'orderedIds': orderedIds},
        _values,
        failureMessage: 'Failed to reorder the ${kind.name}',
      );

  @override
  Future<void> delete(LookupKind kind, String id, {String? replacementId}) =>
      _api.delete(
        '${kind.path}/$id',
        query: replacementId == null ? null : {'replacementId': replacementId},
        failureMessage: 'Failed to delete the ${kind.singular}',
      );
}
