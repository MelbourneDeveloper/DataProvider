/// A byte-offset range in source text.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct Span {
    pub start: u32,
    pub end: u32,
}

impl Span {
    pub fn new(start: u32, end: u32) -> Self {
        Self { start, end }
    }

    pub fn empty(offset: u32) -> Self {
        Self {
            start: offset,
            end: offset,
        }
    }

    pub fn merge(self, other: Span) -> Span {
        Span {
            start: self.start.min(other.start),
            end: self.end.max(other.end),
        }
    }

    /// Convert byte offset to (line, col) where both are 0-based.
    pub fn start_line_col(self, source: &str) -> (u32, u32) {
        byte_offset_to_line_col(source, self.start as usize)
    }

    pub fn end_line_col(self, source: &str) -> (u32, u32) {
        byte_offset_to_line_col(source, self.end as usize)
    }
}

fn byte_offset_to_line_col(source: &str, offset: usize) -> (u32, u32) {
    let mut line = 0u32;
    let mut col = 0u32;
    for (i, ch) in source.char_indices() {
        if i >= offset {
            break;
        }
        if ch == '\n' {
            line += 1;
            col = 0;
        } else {
            col += 1;
        }
    }
    (line, col)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_line_col() {
        let src = "hello\nworld\nfoo";
        assert_eq!(byte_offset_to_line_col(src, 0), (0, 0));
        assert_eq!(byte_offset_to_line_col(src, 5), (0, 5));
        assert_eq!(byte_offset_to_line_col(src, 6), (1, 0));
        assert_eq!(byte_offset_to_line_col(src, 12), (2, 0));
    }

    #[test]
    fn test_merge() {
        let a = Span::new(5, 10);
        let b = Span::new(2, 15);
        assert_eq!(a.merge(b), Span::new(2, 15));
    }

    #[test]
    fn test_merge_identical() {
        let a = Span::new(5, 10);
        assert_eq!(a.merge(a), Span::new(5, 10));
    }

    #[test]
    fn test_merge_non_overlapping() {
        let a = Span::new(1, 3);
        let b = Span::new(7, 9);
        assert_eq!(a.merge(b), Span::new(1, 9));
    }

    #[test]
    fn test_empty_span() {
        let s = Span::empty(42);
        assert_eq!(s.start, 42);
        assert_eq!(s.end, 42);
    }

    #[test]
    fn test_new_span() {
        let s = Span::new(3, 7);
        assert_eq!(s.start, 3);
        assert_eq!(s.end, 7);
    }

    #[test]
    fn test_line_col_multiline() {
        let src = "abc\ndef\nghi";
        // 'g' is at byte 8, line 2 col 0
        assert_eq!(byte_offset_to_line_col(src, 8), (2, 0));
        // 'e' is at byte 4, line 1 col 0
        assert_eq!(byte_offset_to_line_col(src, 4), (1, 0));
        // 'f' is at byte 5, line 1 col 1
        assert_eq!(byte_offset_to_line_col(src, 5), (1, 1));
    }

    #[test]
    fn test_line_col_empty_string() {
        assert_eq!(byte_offset_to_line_col("", 0), (0, 0));
    }

    #[test]
    fn test_line_col_past_end() {
        let src = "ab";
        assert_eq!(byte_offset_to_line_col(src, 100), (0, 2));
    }

    #[test]
    fn test_start_line_col() {
        let src = "hello\nworld";
        let span = Span::new(6, 11);
        assert_eq!(span.start_line_col(src), (1, 0));
    }

    #[test]
    fn test_end_line_col() {
        let src = "hello\nworld";
        let span = Span::new(0, 6);
        assert_eq!(span.end_line_col(src), (1, 0));
    }

    #[test]
    fn test_span_equality() {
        assert_eq!(Span::new(1, 2), Span::new(1, 2));
        assert_ne!(Span::new(1, 2), Span::new(1, 3));
    }

    #[test]
    fn test_span_hash() {
        use std::collections::HashSet;
        let mut set = HashSet::new();
        set.insert(Span::new(1, 2));
        set.insert(Span::new(1, 2));
        assert_eq!(set.len(), 1);
        set.insert(Span::new(3, 4));
        assert_eq!(set.len(), 2);
    }
}
