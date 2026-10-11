pages
- Login and register page
    - Login via email and password text fields (/Auth/login)
    - Button: Register (/Auth/register)

- Home Page
    - A list view of all notes that user has (/NoteGraph/list)
    - Filterable based off of name
    - Button: Create Notegraph (/NoteGraph/create)

- NoteGraph Page
    - Pulls the notegraph (/NoteGraph/id/{notegraphId:Guid})
    - Ability to edit notegraph
        - Add NoteNode (/Notegraph/id/{notegraphId:Guid}/create)
        - Edit NoteNode (/Notegraph/id/{notegraphId:Guid}/edit)
        - Delete NoteNode (/Notegraph/id/{notegraphId:Guid}/delete)

        - Add Tag (/Notegraph/id/{notegraphId:Guid}/tags/create)
        - Edit Tag (/Notegraph/id/{notegraphId:Guid}/tags/edit/{tagId:Guid})
        - Delete Tag (/Notegraph/id/{notegraphId:Guid}/tags/delete/{tagId:Guid})

        - Add Relationship (/Notegraph/id/{notegraphId:Guid}/relationships/create)
        - Edit Relationship (/Notegraph/id/{notegraphId:Guid}/relationships/edit/{relationId:Guid})
        - Delete Relationship (/Notegraph/id/{notegraphId:Guid}/relationships/delete/{relationId:Guid})
    - Search Filter for NoteNode names
    - Filter based off of tags of notenode
    - Button: Export NoteGraphDocument as a json
    - Button: Load up NoteGraphDocument as a D3 graph view of relationships and tags
        - Filter graph by tags
        - Nodes with more relationships are but higher up in depth
    - Button: Back button to return to home page