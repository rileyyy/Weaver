/// What a custom field holds. Mirrors the backend's `CustomFieldKind`,
/// whose names the API sends. Fixed once a field is created.
enum CustomFieldKind {
  text('Text'),
  markdown('Markdown'),
  number('Number'),
  currency('Currency'),
  date('Date'),
  checkbox('Checkbox'),
  singleSelect('Single select'),
  multiSelect('Multi-select'),
  user('User'),
  url('URL');

  const CustomFieldKind(this.label);

  /// How the Admin page names the kind.
  final String label;

  bool get isSelect => this == singleSelect || this == multiSelect;

  /// Whether a field of this kind can roll up into its parents as a sum.
  bool get canRollUp => this == number || this == currency;

  /// The name the API uses, e.g. `SingleSelect`.
  String get apiName => name[0].toUpperCase() + name.substring(1);

  static CustomFieldKind fromApiName(String apiName) =>
      values.firstWhere((kind) => kind.apiName == apiName);
}
