# Lambda Query Language (LQL) Design System

## Brand Identity

### Mission Statement
Lambda Query Language (LQL) is a Functional Programming Language with a similar purpose procedural SQL. It isbuilt on functional programming principles. It transforms complex data operations into elegant, readable pipelines that flow for powerful querying and data transformations.

### Brand Personality
- **Elegant**: Clean, sophisticated, and refined
- **Powerful**: Capable of handling complex data transformations
- **Functional**: Embraces pure functional programming paradigms
- **Modern**: Cutting-edge approach to data querying
- **Intuitive**: Natural flow that mirrors human thought processes

## Visual Identity

### Logo Concept
The LQL logo combines the mathematical lambda symbol (λ) with flowing pipeline elements, representing the functional nature and data flow paradigm of the language.

### Color Palette

#### Primary Colors
- **Volcanic Orange**: `#FF4500` - Molten orange representing raw computational power
- **Deep Forest**: `#228B22` - Rich forest green symbolizing growth and transformation
- **Obsidian**: `#1C1C1C` - Pure obsidian black for depth and sophistication

#### Secondary Colors
- **Amber Glow**: `#FFA500` - Warm amber for highlights and success states
- **Charcoal**: `#36454F` - Deep charcoal for code and secondary text
- **Ivory**: `#FFFFF0` - Warm ivory background with subtle elegance
- **Electric Violet**: `#8A2BE2` - Bold violet for interactive elements

#### Neutral Palette
- **Pure White**: `#FFFFFF`
- **Light Gray**: `#E5E7EB`
- **Medium Gray**: `#9CA3AF`
- **Dark Gray**: `#1F2937`
- **Pure Black**: `#000000`

### Typography

#### Primary Font: Inter
- **Usage**: Headers, UI elements, body text
- **Characteristics**: Modern, clean, highly readable
- **Weights**: 300 (Light), 400 (Regular), 500 (Medium), 600 (SemiBold), 700 (Bold)

#### Code Font: JetBrains Mono
- **Usage**: Code examples, syntax highlighting
- **Characteristics**: Monospace, developer-friendly, excellent readability
- **Weights**: 400 (Regular), 500 (Medium), 700 (Bold)

### Iconography

#### Style Guidelines
- **Style**: Minimalist, geometric, consistent stroke width
- **Weight**: 2px stroke weight
- **Corners**: Rounded (4px radius)
- **Size**: 16px, 20px, 24px, 32px standard sizes

#### Core Icons
- Lambda (λ) symbol
- Pipeline arrows (|>)
- Data flow diagrams
- Function symbols
- Join/Union operations
- Filter/Transform operations

## Layout & Spacing

### Grid System
- **Base Unit**: 8px
- **Container Max Width**: 1200px
- **Breakpoints**:
  - Mobile: 320px - 767px
  - Tablet: 768px - 1023px
  - Desktop: 1024px+

### Spacing Scale
- **xs**: 4px
- **sm**: 8px
- **md**: 16px
- **lg**: 24px
- **xl**: 32px
- **2xl**: 48px
- **3xl**: 64px
- **4xl**: 96px

## Components

### Code Blocks

#### Syntax Highlighting Theme: "LQL Dark"
```css
.lql-code {
  background: #1E293B;
  color: #E2E8F0;
  border-radius: 8px;
  padding: 24px;
  font-family: 'JetBrains Mono', monospace;
  line-height: 1.6;
}

.lql-keyword { color: #FF4500; } /* let, fn, etc. */
.lql-operator { color: #228B22; } /* |>, =>, = */
.lql-function { color: #FFA500; } /* join, filter, select */
.lql-string { color: #8A2BE2; } /* 'completed' */
.lql-comment { color: #64748B; } /* -- comments */
.lql-identifier { color: #E2E8F0; } /* table names, columns */
```

### Buttons

#### Primary Button
```css
.btn-primary {
  background: linear-gradient(135deg, #FF4500 0%, #FFA500 100%);
  color: white;
  padding: 12px 24px;
  border-radius: 8px;
  font-weight: 600;
  transition: all 0.2s ease;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(255, 69, 0, 0.4);
}
```

#### Secondary Button
```css
.btn-secondary {
  background: transparent;
  color: #228B22;
  border: 2px solid #228B22;
  padding: 10px 22px;
  border-radius: 8px;
  font-weight: 600;
  transition: all 0.2s ease;
}

.btn-secondary:hover {
  background: #228B22;
  color: white;
}
```

### Cards

#### Feature Card
```css
.feature-card {
  background: white;
  border-radius: 12px;
  padding: 32px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
  border: 1px solid #E5E7EB;
  transition: all 0.3s ease;
}

.feature-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.12);
}
```

## Animation & Motion

### Principles
- **Purposeful**: Every animation serves a functional purpose
- **Smooth**: Easing functions create natural movement
- **Fast**: Animations complete within 200-300ms
- **Consistent**: Same timing and easing across components

### Easing Functions
- **Standard**: `cubic-bezier(0.4, 0.0, 0.2, 1)`
- **Decelerate**: `cubic-bezier(0.0, 0.0, 0.2, 1)`
- **Accelerate**: `cubic-bezier(0.4, 0.0, 1, 1)`

### Common Animations
- **Fade In**: `opacity: 0 → 1` (200ms)
- **Slide Up**: `transform: translateY(20px) → translateY(0)` (250ms)
- **Scale**: `transform: scale(0.95) → scale(1)` (200ms)
- **Pipeline Flow**: Custom animation for data flow visualization

## Voice & Tone

### Writing Style
- **Clear**: Simple, direct language
- **Technical**: Precise terminology for developers
- **Confident**: Assertive about capabilities
- **Approachable**: Friendly but professional

### Code Examples
Always include:
- Clear comments explaining the operation
- Real-world use cases
- Progressive complexity (simple → advanced)
- Consistent formatting and indentation

## Accessibility

### Color Contrast
- **AA Compliance**: Minimum 4.5:1 contrast ratio
- **AAA Preferred**: 7:1 contrast ratio where possible

### Focus States
```css
.focus-visible {
  outline: 2px solid #FF4500;
  outline-offset: 2px;
  border-radius: 4px;
}
```

### Screen Reader Support
- Semantic HTML structure
- ARIA labels for complex components
- Alt text for all images and icons
- Keyboard navigation support

## Implementation Guidelines

### CSS Custom Properties
```css
:root {
  /* Colors */
  --lql-volcanic: #FF4500;
  --lql-forest: #228B22;
  --lql-obsidian: #1C1C1C;
  --lql-amber: #FFA500;
  --lql-violet: #8A2BE2;
  --lql-charcoal: #36454F;
  
  /* Spacing */
  --space-xs: 4px;
  --space-sm: 8px;
  --space-md: 16px;
  --space-lg: 24px;
  --space-xl: 32px;
  
  /* Typography */
  --font-primary: 'Inter', sans-serif;
  --font-code: 'JetBrains Mono', monospace;
  
  /* Shadows */
  --shadow-sm: 0 2px 8px rgba(0, 0, 0, 0.08);
  --shadow-md: 0 4px 20px rgba(0, 0, 0, 0.12);
  --shadow-lg: 0 8px 40px rgba(0, 0, 0, 0.16);
}
```

### Component Naming Convention
- **BEM Methodology**: `.block__element--modifier`
- **Prefix**: All LQL components prefixed with `lql-`
- **Examples**: `.lql-button`, `.lql-code-block`, `.lql-feature-card`

## Brand Applications

### Website Sections
1. **Hero**: Bold introduction with live code example
2. **Features**: Pipeline visualization, functional benefits
3. **Documentation**: Comprehensive guides with syntax highlighting
4. **Examples**: Real-world use cases and transformations
5. **Community**: Developer resources and contributions

### Marketing Materials
- Consistent use of lambda symbol
- Pipeline flow visualizations
- Code-first approach in all materials
- Professional developer-focused aesthetic

---

*This design system serves as the foundation for all Lambda Query Language brand touchpoints, ensuring consistency and excellence across every user interaction.*