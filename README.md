# GraphNoteLM
A graph based note-taking application for students, researchers, and creatives for relational learning, study, and discovery, powered with AI insights using notebook-enclosed context.


## Why GraphNoteLM?
GraphNoteLM started when I wanted a local relational note taking tool for learning AWS technologies and needing to understand which concepts I was weak on to identify bottlenecks in what I need to study before subsequent concepts.
In addition to each notebook, or "notegraph", being powered by a graph datastructure, you can apply graph algorithms to provide further insights like knowledge spans, hardest concepts to learn, etc. In addition, I liked Google's NotebookLM's utilization of turning note documents as data for LLMs to provide information specific to users whenever need answers specific to the notebook.


## Technologies
Stack
- ReactJS + Vite (JavaScript, HTML & CSS)
- .NET Core 9.0 (C#)
- PostgreSQL
- DynamoDB
- Environment Dockerization
- Redis Caching
Deployment
- AWS ECS Fargate
- AWS RDS
- AWS DynamoDB
- AWS S3 + CloudFront


## Features
### Topic-Agnostic Graph Algorithm Applications
#### Dijkstra's Shortest Path on note nodes' user confidence rating
#### Breadth First Search on minimum confidence rating
#### Topological sort for automatic node sequencing

### Importable and Exportable NoteGraphs as JSON
This gives you to option to create notegraphs without creating an account. Once you are at a good stopping point within the NoteGraph, you're able to export the notegraph as a json and reimport it again to begin writing. In addition, since all information is encapsulated within the JSON, this means that you are not limited to the NoteGraph UI. As long as your UI is able to parse the JSON file, you can create your own views.

### Cloud Storage via DynamoDB
If you want to streamline the saving process of notegraphs, you can utilize the publicly deployed GraphNoteLM. We will handle the storage of your notes securely. Furthermore, you will have access to connected LLMs through a chat, in which has access to conduct graph algorithm and gather notebook-contexted insights for you.

### Ability to Run the Application Locally (Without LLM Capabilities)
If privacy is a big concern to you, a major option is running everything encased within the application within a single docker command. The docker compose will spin up everything - from Frontend, to .NET Backend Service, to even the PostgreSQL and InternalJSONStorage as volumes. All you need is to install docker, clone the repo, and run docker compose --build. The application should be lightweight enough to be run in the background, but contains graceful shutdowns that does not disrupt data.

## Cool Applications of NoteGraphLM
### Learning Pacing Tool
This was the main motivation for me to build this tool, as a way to find the best way to learn concepts and what order to learn them. For example, you can set nodes as concepts, write note and content within them, and then connect each node to other nodes as "prerequisite to" or "has prerequisite" relationships. With the built-in confidence rate within each node's metadata, you can gage how well you have the node's concept learned and whether or not you are ready to move onto the subsequent nodes.

### Worldbuilding and Storytelling for Large Book Projects and Tabletop RPGs
You can use this NoteGraphs instead for learning, but keep an organized and visual representation of your story and in-story world as relationships and concepts. This can be especially useful for people that are into hobbies like Dungeons and Dragons, writing multi-series books that span over many in-story centuries, etc. You can have note nodes be for characters, events, items, and note relationships be connections like "allied with", "enemies with", "caused event", etc.

### Interactive Grid Game
Aside from being a primarily note-taking application, there are ways to make NoteGraphLM an interactable game with the LLM. You can define nodes as tiles or rooms, and separate unconnected nodes as "Characters" which house character specific metadata. The LLM reads your context, identifies story events and can even call BFS to get a list of all exploreable rooms for your character. After each iteration with your chatbot, they update your character's metadata on what room you are on and such.

## Planned Integrations
### WaniKani API and Progress Integration
I am a daily user of WaniKani, which is a flashcards review platform for learning Japanese Kanji and Vocabulary in a curated way. This integration aims to get a list of all the current user's learning kanji and vocab. Relationship edges will be automatically linked between vocab and kanji. This is simply an API call fetch to the WaniKani API and an adapter layer that transforms the data into a NoteGraph document, which then loads to an existing or new NoteGraph.


## Process


## What I Learned


## Options to Run

### Publicly Deployed Service
You can access the service publicly through the url: xxx. You can create an account to have your notes be saved on the cloud. Otherwise, you must handle manual saving by exporting your notegraph every so often (you still have this option as a user).

### Local Development API
Clone the repository from master. Make sure you have the following installed (at the very minimum):
- .NET Core
- PostgreSQL

1. Within .NET application, provide an appsettings based off of appsettings.Example.json, the main crediential being your local postgreSQL server. ("Host=localhost;Port=5432;Database=graphnotelm;Username=postgres;Password=[yourlocalpassword]")
2. CD into the folder that contains all the code within the repo.
3. Apply migrations via Entity Framework: dotnet ef database update
4. Run the application via http or https

### Local Workspace
Clone the repository "feature/with-fe". Make sure you have docker installed.
1. CD into the folder that contains all the code within the repo.
2. run the command: docker compose --build
3. Frontend UI runs on localhost:5173

Caveats:
- You do not have access to the LLM features by default (can be turned back on, but is off due to compliance with everything being ENTIRELY local).
- Shutdown local service
   - run command: docker compose down
   - Go to docker -> Containers -> graphnotelm -> CLick the blue square button, which shuts down the container.
- Delete Internal NoteGraph Data
   - run command: docker volume rm graphnotelm_notegraph_data graphnotelm_postgres_data
   - Go to docker -> Volumes -> Check graphnotelm_notegraph_data and or graphnotelm_postgres_data -> Hit "delete" button on top right
