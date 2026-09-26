/// Small decoders for JSON fields shared by several models.
library;

/// A JSON array of strings; a missing field reads as empty.
List<String> stringList(Object? value) =>
    (value as List<dynamic>?)?.cast<String>().toList() ?? const [];
