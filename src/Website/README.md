# Template Angular Project

Init with :

- Tailwind
- Angular Material

# Tailwind

Possible to add font, add in tailwind.config.js :
```
theme: {
    fontFamily: {
      'wedding': ["Wedding"],
      "windsong": ["WindSong"],
      "librebaskerville": ["LibreBaskerville"],
    },
    extend: {},
  },
```

# Material Angular

## Theme
A custom theme is created in styles.scss: https://material.angular.io/guide/theming#defining-a-theme


# TODO when starting app

It is possible to navigate between the different TODO thanks to the IDE. This is the list of the different changes to do at the init:

## Analytics and SEO

The Website is prepared for Google Analytics 4 (GA4). The tag is loaded only
after the visitor accepts the audience-measurement category in the cookie
banner. It is disabled by default when no measurement ID is configured.

For a deployment:

1. Create a GA4 property and a Web data stream for `https://volepapillondamour.fr`.
2. Copy the measurement ID in the form `G-XXXXXXXXXX`.
3. Add a GitHub Actions environment variable named
   `GOOGLE_ANALYTICS_MEASUREMENT_ID` in the `development` environment.
4. Run the `Website - deploy` workflow. The value is injected into the
   production Angular bundle at build time; it is not required locally.

For the existing Microsoft Clarity project, enable its consent mode / cookie
consent requirement in the Clarity settings. The Website sends the consent v2
signal and revokes it when the visitor changes their choice.

The public SEO files are `public/robots.txt` and `public/sitemap.xml`. Submit
`https://volepapillondamour.fr/sitemap.xml` once in Google Search Console after
the site is deployed. Dynamic actuality and event detail URLs are not listed
because their IDs come from the API; they remain discoverable through the
internal links and their listing pages.

# Name Application

It is important to go in package.json and change the name of the application. 

## Title 

Change in index.html the title of the website
Change in app.component.ts the title of the website

# Icon Website

Add in public/ folder the icon of the app. Change in index.html the path to the icon

# Azurite

```cmd
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```
