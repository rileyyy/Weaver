import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

/// The Admin page's view of one Type's custom fields. Every change needs an
/// admin; the server refuses it otherwise.
abstract class CustomFieldAdminRepository {
  Future<List<CustomField>> list(String typeId);

  /// Appends a field. [optionNames] are a select field's options, in order.
  Future<CustomField> create(
    String typeId,
    String name,
    CustomFieldKind kind,
    List<String> optionNames,
  );

  /// Renames the field and replaces its option list; an option left out of
  /// [options] is removed, and items lose that choice.
  Future<CustomField> update(
    String id,
    String name,
    List<CustomFieldOptionDraft> options,
  );

  /// [orderedIds] must list every field of [typeId] once.
  Future<List<CustomField>> reorder(String typeId, List<String> orderedIds);

  /// Removes the field and every item's value for it.
  Future<void> delete(String id);
}
