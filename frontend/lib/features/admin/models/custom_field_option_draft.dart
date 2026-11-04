/// An option in a select field's edited option list: an existing option
/// (by [id], possibly renamed) or, with no id, a new one.
class CustomFieldOptionDraft {
  const CustomFieldOptionDraft({required this.name, this.id});

  final String? id;
  final String name;

  Map<String, Object?> toJson() => {'id': id, 'name': name};
}
