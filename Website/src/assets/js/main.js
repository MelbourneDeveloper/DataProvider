(function() {
  'use strict';

  // Mobile menu toggle
  const mobileMenuToggle = document.getElementById('mobile-menu-toggle');
  const navLinks = document.querySelector('.nav-links');

  if (mobileMenuToggle && navLinks) {
    mobileMenuToggle.addEventListener('click', () => {
      navLinks.classList.toggle('open');
      mobileMenuToggle.classList.toggle('active');
    });

    document.addEventListener('click', (e) => {
      if (!navLinks.contains(e.target) && !mobileMenuToggle.contains(e.target)) {
        navLinks.classList.remove('open');
        mobileMenuToggle.classList.remove('active');
      }
    });
  }

  // Docs sidebar toggle (mobile)
  const docsSidebar = document.getElementById('docs-sidebar');
  if (docsSidebar) {
    const sidebarToggle = document.createElement('button');
    sidebarToggle.className = 'btn btn-primary';
    sidebarToggle.innerHTML = 'Menu';
    sidebarToggle.style.cssText = 'display: none; position: fixed; bottom: 1rem; right: 1rem; z-index: 60;';
    document.body.appendChild(sidebarToggle);

    const checkMobile = () => {
      sidebarToggle.style.display = window.innerWidth <= 1024 ? 'block' : 'none';
    };
    checkMobile();
    window.addEventListener('resize', checkMobile);

    sidebarToggle.addEventListener('click', () => {
      docsSidebar.classList.toggle('open');
      sidebarToggle.innerHTML = docsSidebar.classList.contains('open') ? 'Close' : 'Menu';
    });
  }

  // Smooth scroll for anchor links
  document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function(e) {
      const target = document.querySelector(this.getAttribute('href'));
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: 'smooth' });
      }
    });
  });

  // Copy button for code blocks
  document.querySelectorAll('pre').forEach(pre => {
    const wrapper = document.createElement('div');
    wrapper.style.position = 'relative';
    pre.parentNode.insertBefore(wrapper, pre);
    wrapper.appendChild(pre);

    const copyBtn = document.createElement('button');
    copyBtn.innerHTML = 'Copy';
    copyBtn.style.cssText = 'position: absolute; top: 0.5rem; right: 0.5rem; padding: 0.25rem 0.75rem; font-size: 0.75rem; background: var(--bg-tertiary); border: 1px solid var(--border-color); border-radius: 4px; cursor: pointer; opacity: 0; transition: opacity 0.2s;';

    wrapper.appendChild(copyBtn);
    wrapper.addEventListener('mouseenter', () => copyBtn.style.opacity = '1');
    wrapper.addEventListener('mouseleave', () => copyBtn.style.opacity = '0');

    copyBtn.addEventListener('click', async () => {
      const code = pre.querySelector('code');
      const text = code ? code.textContent : pre.textContent;
      try {
        await navigator.clipboard.writeText(text);
        copyBtn.innerHTML = 'Copied!';
        setTimeout(() => copyBtn.innerHTML = 'Copy', 2000);
      } catch (err) {
        copyBtn.innerHTML = 'Failed';
      }
    });
  });
})();
