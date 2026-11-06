import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/custom_field_option.dart';

/// A field a Type defines (the backend's `CustomFieldDto`). An item's value
/// for it is in `WorkItem.fields` under [id].
class CustomField {
  const CustomField({
    required this.id,
    required this.typeId,
    required this.name,
    required this.kind,
    required this.order,
    this.options = const [],
    this.rollsUp = false,
  });

  factory CustomField.fromJson(Map<String, dynamic> json) => CustomField(
    id: json['id'] as String,
    typeId: json['typeId'] as String,
    name: json['name'] as String,
    kind: CustomFieldKind.fromApiName(json['kind'] as String),
    order: json['order'] as int,
    options: [
      for (final option in json['options'] as List<dynamic>? ?? const [])
        CustomFieldOption.fromJson(option as Map<String, dynamic>),
    ],
    rollsUp: json['rollsUp'] as bool? ?? false,
  );

  final String id;
  final String typeId;
  final String name;
  final CustomFieldKind kind;
  final int order;

  /// A select field's choices, in order; empty for every other kind.
  final List<CustomFieldOption> options;

  /// Whether a parent shows this field's sum over its descendants. Only a
  /// number or currency field can roll up.
  final bool rollsUp;

  /// The name of [optionId], or null if the field no longer has it.
  String? optionName(String optionId) {
    for (final option in options) {
      if (option.id == optionId) return option.name;
    }
    return null;
  }
}
