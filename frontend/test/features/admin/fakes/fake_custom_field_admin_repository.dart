import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/custom_field_admin_repository.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/custom_field_option.dart';

/// Records each call as a line, and answers as the server would.
class FakeCustomFieldAdminRepository implements CustomFieldAdminRepository {
  FakeCustomFieldAdminRepository(this.fields);

  List<CustomField> fields;
  final List<String> calls = [];

  /// When set, every change fails with it.
  ApiException? error;

  void _failIfSet() {
    if (error case final error?) throw error;
  }

  @override
  Future<List<CustomField>> list(String typeId) async {
    calls.add('list $typeId');
    return fields;
  }

  @override
  Future<CustomField> create(
    String typeId,
    String name,
    CustomFieldKind kind,
    List<String> optionNames,
  ) async {
    calls.add('create $name ${kind.apiName} $optionNames in $typeId');
    _failIfSet();
    return CustomField(
      id: 'field-$name',
      typeId: typeId,
      name: name,
      kind: kind,
      order: fields.length,
      options: [
        for (final option in optionNames)
          CustomFieldOption(id: 'option-$option', name: option),
      ],
    );
  }

  @override
  Future<CustomField> update(
    String id,
    String name,
    List<CustomFieldOptionDraft> options,
  ) async {
    calls.add(
      'update $id $name ${[for (final o in options) '${o.id}:${o.name}']}',
    );
    _failIfSet();
    final field = fields.firstWhere((f) => f.id == id);
    return CustomField(
      id: id,
      typeId: field.typeId,
      name: name,
      kind: field.kind,
      order: field.order,
      options: [
        for (final option in options)
          CustomFieldOption(
            id: option.id ?? 'option-${option.name}',
            name: option.name,
          ),
      ],
    );
  }

  @override
  Future<List<CustomField>> reorder(
    String typeId,
    List<String> orderedIds,
  ) async {
    calls.add('reorder $typeId $orderedIds');
    _failIfSet();
    return [for (final id in orderedIds) fields.firstWhere((f) => f.id == id)];
  }

  @override
  Future<void> delete(String id) async {
    calls.add('delete $id');
    _failIfSet();
  }
}
