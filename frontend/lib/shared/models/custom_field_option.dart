/// One choice of a select field. Values store its [id], so renaming it
/// keeps what items have chosen.
class CustomFieldOption {
  const CustomFieldOption({required this.id, required this.name});

  factory CustomFieldOption.fromJson(Map<String, dynamic> json) =>
      CustomFieldOption(id: json['id'] as String, name: json['name'] as String);

  final String id;
  final String name;
}
