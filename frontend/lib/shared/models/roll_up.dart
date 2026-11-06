import 'package:weaver/shared/models/custom_field_kind.dart';

/// The sum of one roll-up field over an item's sub-items at every depth
/// (the backend's `RollUpDto`). It never includes the item's own value, so
/// the two can be shown side by side.
class RollUp {
  const RollUp({
    required this.fieldId,
    required this.fieldName,
    required this.typeId,
    required this.kind,
    required this.total,
  });

  factory RollUp.fromJson(Map<String, dynamic> json) => RollUp(
    fieldId: json['fieldId'] as String,
    fieldName: json['fieldName'] as String,
    typeId: json['typeId'] as String,
    kind: CustomFieldKind.fromApiName(json['kind'] as String),
    total: json['total'] as num,
  );

  final String fieldId;
  final String fieldName;

  /// The Type that defines the field, which may not be the item's own.
  final String typeId;

  /// Number or currency.
  final CustomFieldKind kind;
  final num total;

  @override
  bool operator ==(Object other) =>
      other is RollUp &&
      other.fieldId == fieldId &&
      other.fieldName == fieldName &&
      other.typeId == typeId &&
      other.kind == kind &&
      other.total == total;

  @override
  int get hashCode => Object.hash(fieldId, fieldName, typeId, kind, total);
}
