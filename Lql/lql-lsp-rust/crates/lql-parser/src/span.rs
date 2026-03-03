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
}
