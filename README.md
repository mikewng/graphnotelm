# GraphNoteLM
A graph based note-taking application for students, researchers, and creatives for relational learning, study, and discovery, powered with AI insights using notebook-enclosed context.


## Why GraphNoteLM?
GraphNoteLM started when I wanted a local relational note taking tool for learning AWS technologies and needing to understand which concepts I was weak on to identify bottlenecks in what I need to study before subsequent concepts.
In addition to each notebook, or "notegraph", being powered by a graph datastructure, you can apply graph algorithms to provide further insights like knowledge spans, hardest concepts to learn, etc. In addition, I liked Google's NotebookLM's utilization of turning note documents as data for LLMs to provide information specific to users whenever need answers specific to the notebook.


## Technologies
- ReactJS + Vite (JavaScript, HTML & CSS)
- ASP.NET 9.0 (C#)
- PostgreSQL
- DynamoDB
- Environment Dockerization
- Redis Caching


## Features

### Importable and Exportable NoteGraphs as JSON
This gives you to option to create notegraphs without creating an account. Once you are at a good stopping point within the NoteGraph, you're able to export the notegraph as a json and reimport it again to begin writing. In addition, since all information is encapsulated within the JSON, this means that you are not limited to the NoteGraph UI. As long as your UI is able to parse the JSON file, you can create your own views.

### Cloud Storage via DynamoDB
If you want to streamline the saving process of notegraphs, you

### Ability to Run the Application Locally (Without LLM Capabilities)
If privacy is a big concern to you, a major option is running everything encased within the application within a single docker command. The docker compose will spin up everything - from Frontend, to .NET Backend Service, to even the PostgreSQL and InternalJSONStorage as volumes. All you need is to install docker, clone the repo, and run docker compose --build. The application should be lightweight enough to be run in the background, but contains graceful shutdowns that does not disrupt data.

## Process

## What I Learned



## Options to Run

### Local Development

### Local Workspace

