import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/admin/data/custom_field_admin_repository.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

@LazySingleton(as: CustomFieldAdminRepository)
class ApiCustomFieldAdminRepository implements CustomFieldAdminRepository {
  ApiCustomFieldAdminRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  static const String _path = '/custom-fields';

  static CustomField _field(Object? json) =>
      CustomField.fromJson(json as Map<String, dynamic>);

  static List<CustomField> _fields(Object? json) =>
      JsonApiClient.listOf(json, CustomField.fromJson);

  @override
  Future<List<CustomField>> list(String typeId) => _api.get(
    _path,
    _fields,
    query: {'typeId': typeId},
    failureMessage: 'Failed to load fields',
  );

  @override
  Future<CustomField> create(
    String typeId,
    String name,
    CustomFieldKind kind,
    List<String> optionNames,
  ) => _api.post(
    _path,
    {
      'typeId': typeId,
      'name': name,
      'kind': kind.apiName,
      'options': optionNames,
    },
    _field,
    failureMessage: 'Failed to add the field',
  );

  @override
  Future<CustomField> update(
    String id,
    String name,
    List<CustomFieldOptionDraft> options,
  ) => _api.put(
    '$_path/$id',
    {
      'name': name,
      'options': [for (final option in options) option.toJson()],
    },
    _field,
    failureMessage: 'Failed to save the field',
  );

  @override
  Future<List<CustomField>> reorder(String typeId, List<String> orderedIds) =>
      _api.put(
        '$_path/order',
        {'orderedIds': orderedIds},
        _fields,
        query: {'typeId': typeId},
        failureMessage: 'Failed to reorder the fields',
      );

  @override
  Future<void> delete(String id) =>
      _api.delete('$_path/$id', failureMessage: 'Failed to remove the field');
}
